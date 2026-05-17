using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.UserData;
using TableCloth.Models.WindowsSandbox;

namespace Spork.Components.Implementations
{
    public sealed class UserDataStore : IUserDataStore
    {
        public UserDataStore(ILogger<UserDataStore> logger)
        {
            _logger = logger;
            _current = new SporkUserData();
        }

        private readonly ILogger _logger;
        private SporkUserData _current;
        private bool _isLoaded;
        private readonly SemaphoreSlim _loadGate = new SemaphoreSlim(1, 1);
        private readonly object _stateLock = new object();
        private CancellationTokenSource _saveDebounceCts;

        private const int SaveDebounceMs = 250;
        private const string SandboxUserName = "WDAGUtilityAccount";
        private const string StandaloneAppDataLeaf = "Spork";

        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            WriteIndented = true,
        };

        public string UserDataFilePath
        {
            get
            {
                var dataDir = ResolveDataDirectory();
                return Path.Combine(dataDir, SporkUserData.FileName);
            }
        }

        public SporkUserData Current => _current;

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
            // 이전 보류분 취소 후 새 토큰 발급. Interlocked.Exchange 로 swap 순서 보장.
            var previous = Interlocked.Exchange(ref _saveDebounceCts, new CancellationTokenSource());
            previous?.Cancel();
            previous?.Dispose();

            var cts = _saveDebounceCts;
            var token = cts.Token;
            SporkUserData snapshot;
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
                catch (OperationCanceledException)
                {
                    // 후속 호출이 supersede 함. 정상 흐름.
                }
            }, token);
        }

        public Task FlushAsync(CancellationToken cancellationToken = default)
        {
            // 보류 디바운스 취소(있다면). 즉시 동기적 스냅샷을 사용해 await.
            var previous = Interlocked.Exchange(ref _saveDebounceCts, null);
            previous?.Cancel();
            previous?.Dispose();

            SporkUserData snapshot;
            lock (_stateLock)
            {
                snapshot = Clone(_current);
            }
            return WriteToDiskAsync(snapshot, cancellationToken);
        }

        private async Task<SporkUserData> LoadFromDiskAsync(CancellationToken cancellationToken)
        {
            var path = UserDataFilePath;
            try
            {
                if (!File.Exists(path))
                    return new SporkUserData();

                using (var stream = File.OpenRead(path))
                {
                    var data = await JsonSerializer.DeserializeAsync<SporkUserData>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
                    return data ?? new SporkUserData();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cannot deserialize user data file: {path}", path);
                return new SporkUserData();
            }
        }

        private async Task WriteToDiskAsync(SporkUserData snapshot, CancellationToken cancellationToken)
        {
            if (snapshot == null)
                return;

            var path = UserDataFilePath;
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
                _logger.LogWarning(ex, "Cannot save user data to: {path}", path);
            }
        }

        private static string ResolveDataDirectory()
        {
            // 샌드박스 안에서는 호스트가 마운트한 Desktop\Data 가 영속 저장소(세션 간 유지).
            // 그 외(단독 Spork.exe 등 비-샌드박스 환경)에선 LocalAppData\Spork 를 사용한다.
            if (IsRunningInWindowsSandbox())
                return SandboxMountPaths.DataDirectory;

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                StandaloneAppDataLeaf);
        }

        private static bool IsRunningInWindowsSandbox()
        {
            // wsb LogonCommand 로 부팅된 샌드박스의 사용자 계정은 WDAGUtilityAccount 로 고정된다.
            // 호스트에서 우연히 같은 계정 이름이 있는 경우를 막기 위해 SandboxDesktop 경로까지 함께 확인.
            return string.Equals(Environment.UserName, SandboxUserName, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(SandboxMountPaths.SandboxDesktop);
        }

        private static SporkUserData Clone(SporkUserData source)
        {
            if (source == null)
                return new SporkUserData();

            return new SporkUserData
            {
                SchemaVersion = source.SchemaVersion,
                ShowFavoritesOnly = source.ShowFavoritesOnly,
                Favorites = source.Favorites != null
                    ? new List<string>(source.Favorites)
                    : new List<string>(),
                LastUsedAt = source.LastUsedAt != null
                    ? new Dictionary<string, DateTime>(source.LastUsedAt)
                    : new Dictionary<string, DateTime>(),
            };
        }
    }
}
