using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.UserData;

namespace Spork.Components.Implementations
{
    public sealed class InstallRecordStore : IInstallRecordStore
    {
        public InstallRecordStore(ILogger<InstallRecordStore> logger)
        {
            _logger = logger;
            _current = new InstallRecord();
        }

        private readonly ILogger _logger;
        private InstallRecord _current;
        private bool _isLoaded;
        private readonly SemaphoreSlim _loadGate = new SemaphoreSlim(1, 1);
        private readonly object _stateLock = new object();
        private CancellationTokenSource _saveDebounceCts;

        private const int SaveDebounceMs = 250;
        private const string AppDataLeaf = "Spork";

        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            WriteIndented = true,
        };

        public string FilePath
        {
            get
            {
                // sandbox/standalone 양쪽에서 자연스럽게 올바른 위치:
                //   - sandbox: WDAGUtilityAccount 의 LocalAppData (VHD 안, 세션 종료 시 사라짐)
                //   - standalone: 실제 사용자 머신 LocalAppData (영속)
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    AppDataLeaf);
                return Path.Combine(dir, InstallRecord.FileName);
            }
        }

        public InstallRecord Current => _current;

        public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
        {
            if (_isLoaded)
                return;

            await _loadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_isLoaded)
                    return;

                _current = await LoadFromDiskAsync(cancellationToken).ConfigureAwait(false);
                _isLoaded = true;
            }
            finally
            {
                _loadGate.Release();
            }
        }

        public void ScheduleSave()
        {
            var previous = Interlocked.Exchange(ref _saveDebounceCts, new CancellationTokenSource());
            previous?.Cancel();
            previous?.Dispose();

            var cts = _saveDebounceCts;
            var token = cts.Token;
            InstallRecord snapshot;
            lock (_stateLock)
            {
                snapshot = Clone(_current);
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(SaveDebounceMs, token).ConfigureAwait(false);
                    await WriteToDiskAsync(snapshot, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { /* superseded by next call */ }
            }, token);
        }

        public Task FlushAsync(CancellationToken cancellationToken = default)
        {
            var previous = Interlocked.Exchange(ref _saveDebounceCts, null);
            previous?.Cancel();
            previous?.Dispose();

            InstallRecord snapshot;
            lock (_stateLock)
            {
                snapshot = Clone(_current);
            }
            return WriteToDiskAsync(snapshot, cancellationToken);
        }

        public bool IsInstalled(string fingerprint)
        {
            if (string.IsNullOrEmpty(fingerprint))
                return false;

            lock (_stateLock)
            {
                return _current?.InstalledFingerprints != null
                    && _current.InstalledFingerprints.Contains(fingerprint);
            }
        }

        public void AddInstalledFingerprint(string fingerprint)
        {
            if (string.IsNullOrEmpty(fingerprint))
                return;

            bool added;
            lock (_stateLock)
            {
                _current.InstalledFingerprints ??= new HashSet<string>(StringComparer.Ordinal);
                added = _current.InstalledFingerprints.Add(fingerprint);
            }

            if (added)
                ScheduleSave();
        }

        public void PruneStaleFingerprints(IEnumerable<string> activeFingerprints)
        {
            int staleCount;
            lock (_stateLock)
            {
                if (_current?.InstalledFingerprints == null || _current.InstalledFingerprints.Count == 0)
                    return;

                var activeSet = activeFingerprints == null
                    ? new HashSet<string>(StringComparer.Ordinal)
                    : new HashSet<string>(activeFingerprints, StringComparer.Ordinal);

                staleCount = _current.InstalledFingerprints.RemoveWhere(fp => !activeSet.Contains(fp));
            }

            if (staleCount > 0)
            {
                _logger.LogDebug("Pruned {count} stale install fingerprints.", staleCount);
                ScheduleSave();
            }
        }

        private async Task<InstallRecord> LoadFromDiskAsync(CancellationToken cancellationToken)
        {
            var path = FilePath;
            try
            {
                if (!File.Exists(path))
                    return new InstallRecord();

                using (var stream = File.OpenRead(path))
                {
                    var data = await JsonSerializer.DeserializeAsync<InstallRecord>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
                    return data ?? new InstallRecord();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cannot deserialize install record file: {path}", path);
                return new InstallRecord();
            }
        }

        private async Task WriteToDiskAsync(InstallRecord snapshot, CancellationToken cancellationToken)
        {
            if (snapshot == null)
                return;

            var path = FilePath;
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                using (var stream = File.Create(path))
                {
                    await JsonSerializer.SerializeAsync(stream, snapshot, SerializerOptions, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cannot save install record to: {path}", path);
            }
        }

        private static InstallRecord Clone(InstallRecord source)
        {
            if (source == null)
                return new InstallRecord();

            return new InstallRecord
            {
                SchemaVersion = source.SchemaVersion,
                InstalledFingerprints = source.InstalledFingerprints != null
                    ? new HashSet<string>(source.InstalledFingerprints, StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal),
            };
        }
    }
}
