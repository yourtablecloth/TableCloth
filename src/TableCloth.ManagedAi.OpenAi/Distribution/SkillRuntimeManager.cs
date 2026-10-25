using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TableCloth.ManagedAi.OpenAi;

internal sealed record SkillRuntimeState(string Id, string ReceiptSha256);
internal sealed record SkillRuntimeReceipt(string Target, string NodeVersion, string NodeSha256,
    string PythonVersion, string PythonSha256, string NpmVersion, Dictionary<string, string> Files);

[JsonSerializable(typeof(SkillRuntimeState))]
[JsonSerializable(typeof(SkillRuntimeReceipt))]
internal partial class SkillRuntimeJsonContext : JsonSerializerContext;

public sealed class SkillRuntimeManager : IManagedAiSkillRuntimeManager
{
    public const string HttpClientName = "TableCloth.SkillRuntimes";
    private const string ReceiptFile = "tablecloth-skill-runtime.json";
    private readonly ManagedAiPaths _paths;
    private readonly IHttpClientFactory _clients;
    private readonly IJsonlProcessRunner _runner;
    private readonly SkillRuntimeManifest _manifest;
    private readonly SkillRuntimeArchiveLimits _limits;

    public SkillRuntimeManager(ManagedAiPaths paths, IHttpClientFactory clients, IJsonlProcessRunner runner,
        SkillRuntimeManifest? manifest = null, SkillRuntimeArchiveLimits? limits = null)
    {
        _paths = paths; _clients = clients; _runner = runner;
        _manifest = manifest ?? SkillRuntimeManifest.ForArchitecture(RuntimeInformation.OSArchitecture);
        _manifest.Validate();
        _limits = limits ?? new();
    }

    // No operation lease: callers may already hold the managed AI lease.
    public async Task<SkillRuntimeEnvironment?> GetActiveAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var state = ReadState();
        return state is null ? null : (await ResolveAsync(state, token)).Environment;
    }

    public async Task<SkillRuntimeEnvironment> InstallAsync(IProgress<AiProgress>? progress, CancellationToken cancellationToken)
    {
        using var lease = _paths.AcquireOperation();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        var token = timeout.Token;
        PrepareToolData();
        var oldState = ReadState();
        if (oldState is not null)
        {
            var active = await ResolveAsync(oldState, token);
            if (active.Receipt.NodeVersion == _manifest.NodeJs.Version && active.Receipt.NodeSha256 == _manifest.NodeJs.Sha256 &&
                active.Receipt.PythonVersion == _manifest.Python.Version && active.Receipt.PythonSha256 == _manifest.Python.Sha256 &&
                active.Receipt.NpmVersion == _manifest.NpmVersion)
            {
                progress?.Report(new("SkillRuntimeVerifying"));
                await VerifyVersionsAsync(active.Environment, token);
                return active.Environment;
            }
        }
        Directory.CreateDirectory(_paths.SkillRuntimeReleases);
        var id = Guid.NewGuid().ToString("N");
        var staging = _paths.Under("runtimes", "skill-tools", "releases", ".staging-" + id);
        var final = ReleaseRoot(id);
        var cache = _paths.Under("cache", "skill-tools", id);
        var moved = false;
        var committed = false;
        try
        {
            if (Directory.Exists(staging) || Directory.Exists(final) || Directory.Exists(cache)) throw Integrity();
            Directory.CreateDirectory(staging);
            Directory.CreateDirectory(cache);
            progress?.Report(new("SkillRuntimeDownloadingNode"));
            var nodeZip = Path.Combine(cache, "node.zip");
            await DownloadAsync(_manifest.NodeJs, nodeZip, token);
            progress?.Report(new("SkillRuntimeDownloadingPython"));
            var pythonZip = Path.Combine(cache, "python.zip");
            await DownloadAsync(_manifest.Python, pythonZip, token);
            progress?.Report(new("SkillRuntimeExtracting"));
            var nodeRoot = Path.Combine(staging, "node");
            var pythonRoot = Path.Combine(staging, "python");
            Directory.CreateDirectory(nodeRoot);
            Directory.CreateDirectory(pythonRoot);
            await using (var archive = new FileStream(nodeZip, FileMode.Open, FileAccess.Read, FileShare.Read))
                await SafeSkillRuntimeZip.ExtractAsync(archive, nodeRoot, _manifest.NodeJs.ArchivePrefix, _limits, token);
            await using (var archive = new FileStream(pythonZip, FileMode.Open, FileAccess.Read, FileShare.Read))
                await SafeSkillRuntimeZip.ExtractAsync(archive, pythonRoot, "", _limits, token);
            ValidateLayout(staging, _manifest.PythonStem);
            ValidatePythonIsolation(pythonRoot, _manifest.PythonStem);
            File.Copy(Path.Combine(pythonRoot, "python.exe"), Path.Combine(pythonRoot, "python3.exe"), overwrite: false);
            var npm = ReadNpmVersion(nodeRoot);
            if (npm != _manifest.NpmVersion) throw VersionMismatch();
            var environment = EnvironmentAt(staging, _manifest.NodeJs.Version, _manifest.Python.Version);
            progress?.Report(new("SkillRuntimeVerifying"));
            await VerifyVersionsAsync(environment, token);
            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var relative in CriticalFiles(_manifest.PythonStem))
                files.Add(relative, await HashFileAsync(OwnedFile(staging, relative), token));
            var receipt = new SkillRuntimeReceipt(_manifest.NodeJs.Target, _manifest.NodeJs.Version, _manifest.NodeJs.Sha256,
                _manifest.Python.Version, _manifest.Python.Sha256, npm, files);
            var receiptBytes = JsonSerializer.SerializeToUtf8Bytes(receipt, SkillRuntimeJsonContext.Default.SkillRuntimeReceipt);
            WriteNewFile(Path.Combine(staging, ReceiptFile), receiptBytes);
            var state = new SkillRuntimeState(id, Convert.ToHexStringLower(SHA256.HashData(receiptBytes)));
            token.ThrowIfCancellationRequested();
            Directory.Move(staging, final);
            moved = true;
            var resolved = await ResolveAsync(state, token);
            await VerifyVersionsAsync(resolved.Environment, token);
            progress?.Report(new("SkillRuntimeActivating"));
            token.ThrowIfCancellationRequested();
            AtomicActivate(state);
            committed = true;
            return resolved.Environment;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new ManagedAiException(AiFailureCode.ProviderTimeout); }
        catch (PathTooLongException)
        { throw new ManagedAiException(AiFailureCode.PathLengthUnsupported); }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        { throw new ManagedAiException(AiFailureCode.OfficialMetadataUnavailable); }
        finally
        {
            SkillDirectoryCleanup.DeleteOwnedBestEffort(_paths, staging);
            SkillDirectoryCleanup.DeleteOwnedBestEffort(_paths, cache);
            if (moved && !committed) SkillDirectoryCleanup.DeleteOwnedBestEffort(_paths, final);
        }
    }

    private async Task DownloadAsync(SkillRuntimePackage package, string target, CancellationToken token)
    {
        using var client = _clients.CreateClient(HttpClientName);
        using var response = await client.GetAsync(package.Url, HttpCompletionOption.ResponseHeadersRead, token);
        // The registered client MUST disable redirects and credentials. Do not follow Location/download URLs.
        if (response.StatusCode != System.Net.HttpStatusCode.OK) throw new HttpRequestException();
        if (response.Content.Headers.ContentLength > _limits.MaxDownloadBytes) throw new ManagedAiException(AiFailureCode.OutputLimitExceeded);
        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long total = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, token)) != 0)
        {
            total += count;
            if (total > _limits.MaxDownloadBytes) throw new ManagedAiException(AiFailureCode.OutputLimitExceeded);
            hash.AppendData(buffer, 0, count);
            await output.WriteAsync(buffer.AsMemory(0, count), token);
        }
        if (!Convert.ToHexString(hash.GetHashAndReset()).Equals(package.Sha256, StringComparison.OrdinalIgnoreCase)) throw Integrity();
    }

    private async Task<(SkillRuntimeEnvironment Environment, SkillRuntimeReceipt Receipt)> ResolveAsync(SkillRuntimeState state, CancellationToken token)
    {
        var root = ReleaseRoot(state.Id);
        var receiptPath = OwnedFile(root, ReceiptFile);
        if (!File.Exists(receiptPath) || new FileInfo(receiptPath).Length > 65536 || !ValidHash(state.ReceiptSha256)) throw Integrity();
        var bytes = await File.ReadAllBytesAsync(receiptPath, token);
        if (!Convert.ToHexStringLower(SHA256.HashData(bytes)).Equals(state.ReceiptSha256, StringComparison.OrdinalIgnoreCase)) throw Integrity();
        try
        {
            var receipt = JsonSerializer.Deserialize(bytes, SkillRuntimeJsonContext.Default.SkillRuntimeReceipt) ?? throw Integrity();
            var manifest = ManifestFromReceipt(receipt);
            manifest.Validate();
            if (receipt.Target != _manifest.NodeJs.Target) throw Integrity();
            var required = CriticalFiles(manifest.PythonStem);
            if (receipt.Files.Count != required.Length || receipt.Files.Keys.Except(required, StringComparer.Ordinal).Any()) throw Integrity();
            ValidateLayout(root, manifest.PythonStem);
            ValidatePythonIsolation(Path.Combine(root, "python"), manifest.PythonStem);
            foreach (var pair in receipt.Files)
                if (!ValidHash(pair.Value) || !pair.Value.Equals(await HashFileAsync(OwnedFile(root, pair.Key), token), StringComparison.OrdinalIgnoreCase)) throw Integrity();
            if (!receipt.Files["python/python.exe"].Equals(receipt.Files["python/python3.exe"], StringComparison.OrdinalIgnoreCase)) throw Integrity();
            return (EnvironmentAt(root, receipt.NodeVersion, receipt.PythonVersion), receipt);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or NullReferenceException or KeyNotFoundException)
        { throw Integrity(); }
    }

    private static SkillRuntimeManifest ManifestFromReceipt(SkillRuntimeReceipt receipt)
    {
        var prefix = $"node-v{receipt.NodeVersion}-{receipt.Target}";
        return new(new(receipt.NodeVersion, receipt.Target, receipt.NodeSha256, new($"https://nodejs.org/dist/v{receipt.NodeVersion}/{prefix}.zip"), prefix),
            new(receipt.PythonVersion, receipt.Target, receipt.PythonSha256,
                new($"https://www.python.org/ftp/python/{receipt.PythonVersion}/python-{receipt.PythonVersion}-embed-{(receipt.Target == "win-x64" ? "amd64" : "arm64")}.zip"), ""))
        { NpmVersion = receipt.NpmVersion };
    }

    private async Task VerifyVersionsAsync(SkillRuntimeEnvironment environment, CancellationToken token)
    {
        await VerifyAsync(environment.NodeJs.EntryPoint, ["--version"], "v" + environment.NodeJs.Version, token);
        await VerifyAsync(environment.Python.EntryPoint, ["-I", "-S", "--version"], "Python " + environment.Python.Version, token);
        await VerifyAsync(environment.NodeJs.EntryPoint, [Path.Combine(environment.NodeJs.RootDirectory, "node_modules", "npm", "bin", "npm-cli.js"), "--version"], _manifest.NpmVersion, token);
        await VerifyAsync(environment.NodeJs.EntryPoint, [Path.Combine(environment.NodeJs.RootDirectory, "node_modules", "npm", "bin", "npx-cli.js"), "--version"], _manifest.NpmVersion, token);
    }

    private async Task VerifyAsync(string executable, string[] arguments, string expected, CancellationToken token)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false, true), StandardErrorEncoding = new UTF8Encoding(false, true),
            WorkingDirectory = _paths.SkillToolData
        };
        start.Environment.Clear();
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        start.Environment["SystemRoot"] = windows;
        start.Environment["WINDIR"] = windows;
        start.Environment["TEMP"] = _paths.Under("profiles", "skill-tools", "temp");
        start.Environment["TMP"] = start.Environment["TEMP"];
        start.Environment["HOME"] = _paths.Under("profiles", "skill-tools", "home");
        start.Environment["USERPROFILE"] = start.Environment["HOME"];
        start.Environment["APPDATA"] = start.Environment["HOME"];
        start.Environment["LOCALAPPDATA"] = start.Environment["HOME"];
        start.Environment["NPM_CONFIG_CACHE"] = _paths.Under("profiles", "skill-tools", "npm-cache");
        start.Environment["NPM_CONFIG_PREFIX"] = _paths.Under("profiles", "skill-tools", "npm-prefix");
        start.Environment["NPM_CONFIG_USERCONFIG"] = _paths.Under("profiles", "skill-tools", "user.npmrc");
        start.Environment["NPM_CONFIG_GLOBALCONFIG"] = _paths.Under("profiles", "skill-tools", "global.npmrc");
        start.Environment["PYTHONNOUSERSITE"] = "1";
        start.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
        start.Environment["PYTHONUTF8"] = "1";
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        var output = new StringBuilder();
        var spec = new ProcessRunSpec(start, null, TimeSpan.FromSeconds(20), 16384, 16384, 8192);
        var result = await _runner.RunAsync(spec, line => output.AppendLine(line), line => output.AppendLine(line), token);
        if (result.ExitCode != 0 || output.ToString().Trim() != expected) throw VersionMismatch();
    }

    private void PrepareToolData()
    {
        foreach (var name in new[] { "temp", "home", "npm-cache", "npm-prefix" })
            Directory.CreateDirectory(_paths.Under("profiles", "skill-tools", name));
        foreach (var name in new[] { "user.npmrc", "global.npmrc" })
            File.WriteAllText(_paths.Under("profiles", "skill-tools", name), "", new UTF8Encoding(false));
    }

    private SkillRuntimeState? ReadState()
    {
        var path = _paths.SkillRuntimeCurrent;
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 4096) throw Integrity();
        try
        {
            var state = JsonSerializer.Deserialize(File.ReadAllText(path), SkillRuntimeJsonContext.Default.SkillRuntimeState);
            if (state is null || string.IsNullOrEmpty(state.Id) || !ValidHash(state.ReceiptSha256)) throw Integrity();
            return state;
        }
        catch (JsonException) { throw Integrity(); }
    }

    private void AtomicActivate(SkillRuntimeState state)
    {
        var temporary = _paths.Under("runtimes", "skill-tools", "current-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            WriteNewFile(temporary, JsonSerializer.SerializeToUtf8Bytes(state, SkillRuntimeJsonContext.Default.SkillRuntimeState));
            if (File.Exists(_paths.SkillRuntimeCurrent)) File.Replace(temporary, _paths.SkillRuntimeCurrent, null);
            else File.Move(temporary, _paths.SkillRuntimeCurrent);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private string ReleaseRoot(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length != 32 || !id.All(char.IsAsciiHexDigit)) throw Integrity();
        return _paths.Under("runtimes", "skill-tools", "releases", id);
    }
    private static SkillRuntimeEnvironment EnvironmentAt(string root, string nodeVersion, string pythonVersion) =>
        new(new(nodeVersion, Path.Combine(root, "node"), Path.Combine(root, "node", "node.exe")),
            new(pythonVersion, Path.Combine(root, "python"), Path.Combine(root, "python", "python.exe")));

    private static string[] CriticalFiles(string stem) =>
        ["node/node.exe", "node/npm.cmd", "node/npx.cmd", "node/node_modules/npm/bin/npm-cli.js", "node/node_modules/npm/bin/npx-cli.js",
         "node/node_modules/npm/package.json", "python/python.exe", "python/python3.exe", "python/python3.dll", $"python/{stem}.dll", $"python/{stem}.zip", $"python/{stem}._pth"];

    private static void ValidateLayout(string root, string stem)
    {
        foreach (var relative in CriticalFiles(stem).Where(x => x != "python/python3.exe"))
        {
            var path = OwnedFile(root, relative);
            if (!File.Exists(path) || new FileInfo(path).Length == 0) throw new ManagedAiException(AiFailureCode.ArchiveRejected);
        }
    }
    private static void ValidatePythonIsolation(string root, string stem)
    {
        var path = OwnedFile(root, stem + "._pth");
        if (new FileInfo(path).Length > 4096) throw Integrity();
        var lines = File.ReadAllLines(path, new UTF8Encoding(false, true)).Select(x => x.Trim())
            .Where(x => x.Length != 0 && !x.StartsWith('#')).ToArray();
        if (!lines.SequenceEqual(new[] { stem + ".zip", "." }, StringComparer.Ordinal))
            throw new ManagedAiException(AiFailureCode.BlockedByPolicy);
    }
    private static string ReadNpmVersion(string root)
    {
        var path = OwnedFile(root, "node_modules/npm/package.json");
        if (new FileInfo(path).Length > 65536) throw Integrity();
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { MaxDepth = 8 });
            return document.RootElement.GetProperty("version").GetString() ?? throw Integrity();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException) { throw Integrity(); }
    }
    private static string OwnedFile(string root, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (full.Length > SafeSkillRuntimeZip.MaxTargetPathLength || !full.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ManagedAiException(AiFailureCode.InvalidPath);
        ManagedAiPaths.RejectReparsePoints(full);
        return full;
    }
    private static async Task<string> HashFileAsync(string path, CancellationToken token)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(file, token));
    }
    private static void WriteNewFile(string path, byte[] bytes)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        file.Write(bytes);
        file.Flush(flushToDisk: true);
    }
    private static bool ValidHash(string? hash) => hash is { Length: 64 } && hash.All(char.IsAsciiHexDigit);
    private static ManagedAiException Integrity() => new(AiFailureCode.IntegrityMismatch);
    private static ManagedAiException VersionMismatch() => new(AiFailureCode.RuntimeVersionMismatch);
}
