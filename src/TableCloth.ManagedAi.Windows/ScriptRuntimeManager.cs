using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using TableCloth.AppContainerPoc;

namespace TableCloth.ManagedAi.Windows;

public sealed record ScriptRuntimeFile(string Path, string Sha256);

public interface IManagedAiScriptExecutionRuntime : IManagedAiScriptRuntimeManager
{
    string PythonExecutable { get; }
    string NodeExecutable { get; }
    Task<IReadOnlyList<ScriptRuntimeFile>> GetVerifiedFilesAsync(CancellationToken token);
}

/// <summary>App-owned, pinned workers. No global PATH, package manager, registry or shell configuration is changed.</summary>
public sealed class WindowsManagedAiScriptRuntimeManager(ManagedAiPaths paths, IHttpClientFactory clients) : IManagedAiScriptExecutionRuntime
{
    public const string DownloadHttpClientName = "TableCloth.ManagedAi.ScriptRuntimeDownload";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private const long MaximumArchiveBytes = 128L * 1024 * 1024;
    private readonly ScriptRuntimePackages _packages = ScriptRuntimePackages.ForArchitecture(RuntimeInformation.OSArchitecture);

    public Task<AiScriptRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken)
        => RunAsync(install: false, cancellationToken);

    public Task<AiScriptRuntimeStatus> EnsureInstalledAsync(CancellationToken cancellationToken)
        => RunAsync(install: true, cancellationToken);

    public string PythonExecutable => Path.Combine(RuntimeDirectory(_packages.Python), "python.exe");
    public string NodeExecutable => Path.Combine(RuntimeDirectory(_packages.Node), "node.exe");

    private async Task<AiScriptRuntimeStatus> RunAsync(bool install, CancellationToken token)
    {
        await Gate.WaitAsync(token);
        try
        {
            var python = await PrepareComponentAsync(_packages.Python, install, token);
            var node = await PrepareComponentAsync(_packages.Node, install, token);
            bool? pythonLong = null, nodeLong = null;
            string? diagnosticFailure = null;
            if (python.Installed)
            {
                var probe = await ProbeAsync(_packages.Python, token);
                pythonLong = probe.LongPaths;
                python = python with { InstalledVersion = probe.Version, Failure = probe.Failure,
                    Installed = probe.Version == _packages.Python.Version };
                diagnosticFailure ??= probe.Failure;
            }
            if (node.Installed)
            {
                var probe = await ProbeAsync(_packages.Node, token);
                nodeLong = probe.LongPaths;
                node = node with { InstalledVersion = probe.Version, Failure = probe.Failure,
                    Installed = probe.Version == _packages.Node.Version };
                diagnosticFailure ??= probe.Failure;
            }
            if (install && python.Installed && node.Installed)
                WriteCurrentReceipt(python, node, pythonLong, nodeLong);
            return new(python, node, new(pythonLong, nodeLong, diagnosticFailure));
        }
        finally { Gate.Release(); }
    }

    private async Task<AiScriptRuntimeComponentStatus> PrepareComponentAsync(ScriptRuntimePackage package, bool install, CancellationToken token)
    {
        try
        {
            if (install)
            {
                Directory.CreateDirectory(paths.Under("skill-execution"));
                // A canceled chat can terminate its broker before its finally block runs.
                // Recover only after the native runner obtains its own cross-process lease.
                // Do this before acquiring the installation lease to avoid a recursive lock.
                var executionRoot = paths.Under("skill-execution");
                if (Directory.EnumerateFiles(executionRoot, ".acl-journal-*.json").Any() &&
                    AppContainerProcessRunner.RecoverPendingAclJournals(executionRoot) is not null)
                    throw new ManagedAiException(AiFailureCode.BlockedByPolicy);
                // Same cross-process lease used by the native runner: never repair files while ACL grants/workers are active.
                await using var lease = new FileStream(paths.Under("skill-execution", ".appcontainer-acl.lock"),
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                if (Directory.EnumerateFiles(paths.Under("skill-execution"), ".acl-journal-*.json").Any())
                    throw new ManagedAiException(AiFailureCode.BlockedByPolicy);
                await EnsurePackageAsync(package, token);
            }
            if (!File.Exists(ArchivePath(package)) || !Directory.Exists(RuntimeDirectory(package)))
                return new(package.Name, package.Version, null, false, "RuntimeNotInstalled");
            await ValidatePackageAsync(package, token);
            return new(package.Name, package.Version, null, true, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (ManagedAiException error) { return new(package.Name, package.Version, null, false, error.Code.ToString()); }
        catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33)
        { return new(package.Name, package.Version, null, false, "RuntimeBusy"); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or HttpRequestException or InvalidDataException)
        { return new(package.Name, package.Version, null, false, error is HttpRequestException ? "OfficialMetadataUnavailable" : "IntegrityMismatch"); }
    }

    private string RuntimeDirectory(ScriptRuntimePackage package) => paths.Under("skill-execution", "runtimes",
        (package.Name == "Python" ? "py-" : "node-") + package.Version + "-" + package.Target);

    private string ArchivePath(ScriptRuntimePackage package) => paths.Under("skill-execution", "archives",
        Path.GetFileName(package.Url.AbsolutePath));

    private async Task EnsurePackageAsync(ScriptRuntimePackage package, CancellationToken token)
    {
        var archive = ArchivePath(package);
        if (!await ArchiveValidAsync(archive, package.Sha256, token))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(archive)!);
            var partial = paths.Under("skill-execution", "archives", Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                using var client = clients.CreateClient(DownloadHttpClientName);
                using var response = await client.GetAsync(package.Url, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is > MaximumArchiveBytes) throw new ManagedAiException(AiFailureCode.ArchiveRejected);
                await using (var input = await response.Content.ReadAsStreamAsync(token))
                await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[65536];
                    long total = 0;
                    int length;
                    while ((length = await input.ReadAsync(buffer, token)) != 0)
                    {
                        if ((total += length) > MaximumArchiveBytes) throw new ManagedAiException(AiFailureCode.ArchiveRejected);
                        await output.WriteAsync(buffer.AsMemory(0, length), token);
                    }
                }
                if (!await ArchiveValidAsync(partial, package.Sha256, token)) throw new ManagedAiException(AiFailureCode.IntegrityMismatch);
                File.Move(partial, archive, overwrite: true);
            }
            finally { if (File.Exists(partial)) File.Delete(partial); }
        }
        try
        {
            await ValidatePackageAsync(package, token);
            return;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) when (error is ManagedAiException or IOException or UnauthorizedAccessException) { }

        var stage = paths.Under("skill-execution", "runtimes", "stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            await using (var zip = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (!Convert.ToHexString(await SHA256.HashDataAsync(zip, token)).Equals(package.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new ManagedAiException(AiFailureCode.IntegrityMismatch);
                zip.Position = 0;
                await SafeScriptRuntimeZip.ExtractAsync(zip, stage, package.ArchivePrefix,
                    new(), token, package.Includes);
            }
            var destination = RuntimeDirectory(package);
            if (Directory.Exists(destination))
            {
                // The directory is a fixed app-owned version path, checked before recursive removal.
                RejectTree(destination);
                paths.DeleteOwnedDirectory(destination);
            }
            Directory.Move(stage, destination);
            await ValidatePackageAsync(package, token);
        }
        finally { if (Directory.Exists(stage)) { RejectTree(stage); paths.DeleteOwnedDirectory(stage); } }
    }

    private static async Task<bool> ArchiveValidAsync(string path, string hash, CancellationToken token)
    {
        ManagedAiPaths.RejectReparsePoints(path);
        if (!File.Exists(path)) return false;
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return file.Length is > 0 and <= MaximumArchiveBytes &&
            Convert.ToHexString(await SHA256.HashDataAsync(file, token)).Equals(hash, StringComparison.OrdinalIgnoreCase);
    }

    // Validate every installed dependency against the pinned ZIP, not a mutable self-reported receipt.
    private async Task<IReadOnlyList<ScriptRuntimeFile>> ValidatePackageAsync(ScriptRuntimePackage package, CancellationToken token)
    {
        var archive = ArchivePath(package);
        ManagedAiPaths.RejectReparsePoints(archive);
        await using var archiveLock = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (archiveLock.Length is <= 0 or > MaximumArchiveBytes ||
            !Convert.ToHexString(await SHA256.HashDataAsync(archiveLock, token)).Equals(package.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new ManagedAiException(AiFailureCode.IntegrityMismatch);
        archiveLock.Position = 0;
        var directory = RuntimeDirectory(package);
        if (!Directory.Exists(directory)) throw new ManagedAiException(AiFailureCode.RuntimeNotInstalled);
        RejectTree(directory);
        var files = new List<ScriptRuntimeFile>();
        using var zip = new ZipArchive(archiveLock, ZipArchiveMode.Read, leaveOpen: true);
        foreach (var entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested();
            var name = entry.FullName;
            if (name.EndsWith('/')) continue;
            if (package.ArchivePrefix.Length != 0)
            {
                if (!name.StartsWith(package.ArchivePrefix + "/", StringComparison.Ordinal)) throw new ManagedAiException(AiFailureCode.ArchiveRejected);
                name = name[(package.ArchivePrefix.Length + 1)..];
            }
            if (!package.Includes(name)) continue;
            var target = Path.GetFullPath(Path.Combine(directory, name));
            if (target.Length > 240 || !target.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ManagedAiException(AiFailureCode.ArchiveRejected);
            ManagedAiPaths.RejectReparsePoints(target);
            await using var installed = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var original = entry.Open();
            var expected = Convert.ToHexStringLower(await SHA256.HashDataAsync(original, token));
            var actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(installed, token));
            if (installed.Length != entry.Length || actual != expected) throw new ManagedAiException(AiFailureCode.IntegrityMismatch);
            files.Add(new(target, expected));
        }
        if (!files.Any(x => Path.GetFileName(x.Path).Equals(package.Executable, StringComparison.OrdinalIgnoreCase)) ||
            Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Count() != files.Count)
            throw new ManagedAiException(AiFailureCode.IntegrityMismatch);
        return files.AsReadOnly();
    }

    public async Task<IReadOnlyList<ScriptRuntimeFile>> GetVerifiedFilesAsync(CancellationToken token)
    {
        var python = await ValidatePackageAsync(_packages.Python, token);
        var node = await ValidatePackageAsync(_packages.Node, token);
        return python.Concat(node).ToArray();
    }

    private void WriteCurrentReceipt(AiScriptRuntimeComponentStatus python, AiScriptRuntimeComponentStatus node,
        bool? pythonLong, bool? nodeLong)
    {
        var receipt = paths.Under("skill-execution", "current-runtimes.json");
        var temporary = paths.Under("skill-execution", "receipt-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new Utf8JsonWriter(output))
            {
                writer.WriteStartObject(); writer.WriteNumber("schema", 1);
                writer.WriteString("architecture", _packages.Python.Target);
                writer.WriteString("verifiedAtUtc", DateTimeOffset.UtcNow);
                Write("python", _packages.Python, python.InstalledVersion, pythonLong);
                Write("node", _packages.Node, node.InstalledVersion, nodeLong);
                writer.WriteEndObject();
                void Write(string name, ScriptRuntimePackage package, string? version, bool? longPaths)
                {
                    writer.WriteStartObject(name);
                    writer.WriteString("version", version); writer.WriteString("archiveSha256", package.Sha256);
                    writer.WriteString("directory", RuntimeDirectory(package));
                    if (longPaths is { } supported) writer.WriteBoolean("longPathsSupported", supported);
                    else writer.WriteNull("longPathsSupported");
                    writer.WriteEndObject();
                }
            }
            File.Move(temporary, receipt, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private async Task<(string? Version, bool? LongPaths, string? Failure)> ProbeAsync(ScriptRuntimePackage package, CancellationToken token)
    {
        var root = paths.Under("skill-execution");
        var probes = paths.Under("skill-execution", "probes");
        Directory.CreateDirectory(probes);
        var python = package.Name == "Python";
        var script = paths.Under("skill-execution", "probes", python ? "runtime-check.py" : "runtime-check.cjs");
        try
        {
            // Both source and dependencies stay locked through the native sandbox execution.
            await File.WriteAllTextAsync(script, python ? PythonProbe : NodeProbe, token);
            await using var scriptLock = new FileStream(script, FileMode.Open, FileAccess.Read, FileShare.Read);
            var dependencies = await ValidatePackageAsync(package, token);
            var locks = new List<FileStream>();
            try
            {
                foreach (var item in dependencies)
                {
                    ManagedAiPaths.RejectReparsePoints(item.Path);
                    var file = new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    locks.Add(file);
                    if (Convert.ToHexStringLower(await SHA256.HashDataAsync(file, token)) != item.Sha256)
                        throw new ManagedAiException(AiFailureCode.IntegrityMismatch);
                }
                var result = await new AppContainerProcessRunner(root).RunAsync(new(
                    Path.Combine(RuntimeDirectory(package), package.Executable),
                    python ? ["-I", "-S", "-B", "-X", "utf8", script] : ["--preserve-symlinks-main", script],
                    probes, "", [RuntimeDirectory(package), probes], [], AllowNetwork: false), token);
                if (!result.WasAppContainer || result.ExitCode != 0 || result.Failure is not null || result.TimedOut || result.OutputLimited)
                    return (null, null, "SkillIsolationFailed");
                using var output = JsonDocument.Parse(result.Stdout);
                var version = output.RootElement.GetProperty("version").GetString();
                if (version != package.Version) return (null, null, "RuntimeVersionMismatch");
                return (version, output.RootElement.GetProperty("longPathsSupported").GetBoolean(), null);
            }
            finally { foreach (var file in locks) await file.DisposeAsync(); }
        }
        catch (OperationCanceledException) { throw; }
        catch (ManagedAiException error) { return (null, null, error.Code.ToString()); }
        catch { return (null, null, "SkillIsolationFailed"); }
    }

    private static void RejectTree(string directory)
    {
        ManagedAiPaths.RejectReparsePoints(directory);
        if (!Directory.Exists(directory)) return;
        var stack = new Stack<string>();
        stack.Push(directory);
        var budget = 8192;
        while (stack.TryPop(out var current))
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                if (--budget < 0 || (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new ManagedAiException(AiFailureCode.IntegrityMismatch);
                if (Directory.Exists(entry)) stack.Push(entry);
            }
    }

    private const string PythonProbe = """
        import os, sys, json
        supported = False
        try:
            path = os.environ['TEMP']
            while len(path) < 320:
                path = os.path.join(path, 'tablecloth-long-path-check')
            os.makedirs(path, exist_ok=True)
            path = os.path.join(path, 'probe.txt')
            with open(path, 'w', encoding='utf-8') as f:
                f.write('TableCloth')
            with open(path, 'r', encoding='utf-8') as f:
                supported = len(path) > 260 and f.read() == 'TableCloth'
        except OSError:
            pass
        print(json.dumps({'version': '.'.join(map(str, sys.version_info[:3])), 'longPathsSupported': supported}))
        """;

    private const string NodeProbe = """
        'use strict';
        const fs = require('node:fs');
        const path = require('node:path');
        let supported = false;
        try {
          let directory = process.env.TEMP;
          while (directory.length < 320) directory = path.join(directory, 'tablecloth-long-path-check');
          fs.mkdirSync(directory, { recursive: true });
          const file = path.join(directory, 'probe.txt');
          fs.writeFileSync(file, 'TableCloth', 'utf8');
          supported = file.length > 260 && fs.readFileSync(file, 'utf8') === 'TableCloth';
        } catch (error) { }
        console.log(JSON.stringify({ version: process.versions.node, longPathsSupported: supported }));
        """;
}
