using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TableCloth.ManagedAi.OpenAi;

public sealed record RuntimeState(string Version, string Target, string PackageSha256, DateTimeOffset ActivatedAtUtc);

[JsonSerializable(typeof(RuntimeState))]
internal partial class RuntimeJsonContext : JsonSerializerContext;

public sealed class OpenAiCodexPackageInstaller(ManagedAiPaths paths, OpenAiReleaseMetadataClient metadata,
    IJsonlProcessRunner runner, IManagedAiProfile profile, ManagedAiOptions options) : IManagedRuntimeManager
{
    public Task<ManagedRuntime?> GetActiveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ReadState(paths.Current) is { } state ? Resolve(state) : null);
    }

    public async Task<ManagedRuntime> InstallAsync(string? version, IProgress<AiProgress>? progress, CancellationToken token)
    {
        using var lease = paths.AcquireOperation();
        profile.Prepare();
        progress?.Report(new("ResolvingRelease"));
        var release = await metadata.GetAsync(version, token);
        var old = ReadState(paths.Current);
        if (old?.Version == release.Version && old.Target == release.Target && old.PackageSha256 == release.Package.Sha256)
        {
            var active = Resolve(old);
            await VerifyVersionAsync(active, token);
            return active;
        }

        Directory.CreateDirectory(paths.Releases);
        Directory.CreateDirectory(paths.Downloads);
        var id = Guid.NewGuid().ToString("N");
        var staging = paths.Under("runtimes", "openai-codex", "releases", $"{release.Version}-{release.Target}.staging.{id}");
        var partial = paths.Under("cache", "downloads", id + ".partial");
        var state = new RuntimeState(release.Version, release.Target, release.Package.Sha256, DateTimeOffset.UtcNow);
        var root = ReleaseRoot(state);
        try
        {
            if (!Directory.Exists(root))
            {
                progress?.Report(new("VerifyingChecksums"));
                using var checksums = new MemoryStream();
                var checksumHash = await metadata.DownloadAsync(release.Checksums.Url, checksums, 1024 * 1024, token);
                if (checksumHash != release.Checksums.Sha256) throw Integrity();
                VerifyChecksumList(Encoding.UTF8.GetString(checksums.ToArray()), release.Package);
                progress?.Report(new("Downloading"));
                await using (var file = new FileStream(partial, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    var hash = await metadata.DownloadAsync(release.Package.Url, file, options.MaxDownloadBytes, token);
                    if (hash != release.Package.Sha256) throw Integrity();
                    file.Position = 0;
                    Directory.CreateDirectory(staging);
                    progress?.Report(new("Extracting"));
                    await SafeCodexArchive.ExtractAsync(file, staging, options, token);
                }
                SafeCodexArchive.ValidateLayout(staging);
                await VerifyVersionAsync(ToRuntime(state, staging), token);
                AtomicWrite(Path.Combine(staging, "tablecloth-receipt.json"), state);
                Directory.Move(staging, root);
            }
            else
            {
                var receipt = ReadState(Path.Combine(root, "tablecloth-receipt.json"));
                if (receipt is null || receipt.Version != state.Version || receipt.Target != state.Target ||
                    receipt.PackageSha256 != state.PackageSha256) throw Integrity();
            }
            var runtime = Resolve(state);
            await VerifyVersionAsync(runtime, token);
            progress?.Report(new("Activating"));
            await ActivateAsync(state, token);
            return runtime;
        }
        finally
        {
            paths.DeleteOwnedDirectory(staging);
            if (File.Exists(partial)) File.Delete(partial);
        }
    }

    public async Task<ManagedRuntime> RollbackAsync(CancellationToken token)
    {
        using var lease = paths.AcquireOperation();
        profile.Prepare();
        var previous = ReadState(paths.Previous) ?? throw new ManagedAiException(AiFailureCode.NoPreviousVersion);
        var runtime = Resolve(previous);
        await VerifyVersionAsync(runtime, token);
        var state = previous with { ActivatedAtUtc = DateTimeOffset.UtcNow };
        await ActivateAsync(state, token);
        return Resolve(state);
    }

    private async Task ActivateAsync(RuntimeState state, CancellationToken token)
    {
        var current = ReadState(paths.Current);
        var previous = ReadState(paths.Previous);
        Directory.CreateDirectory(paths.State);
        try
        {
            // File.Replace changes current and its backup in one filesystem operation.
            AtomicWrite(paths.Current, state, current is not null ? paths.Previous : null);
            await VerifyVersionAsync(Resolve(state), token);
        }
        catch
        {
            if (current is not null) AtomicWrite(paths.Current, current);
            else File.Delete(paths.Current);
            if (previous is not null) AtomicWrite(paths.Previous, previous);
            else File.Delete(paths.Previous);
            throw;
        }
    }

    private async Task VerifyVersionAsync(ManagedRuntime runtime, CancellationToken token)
    {
        var output = new StringBuilder();
        var spec = OpenAiCodexInvocationBuilder.Command(runtime, runtime.ProfileDirectory, ["--version"], TimeSpan.FromSeconds(15));
        var result = await runner.RunAsync(spec, line => output.AppendLine(line), null, token);
        if (result.ExitCode != 0 || output.ToString().Trim() != "codex-cli " + runtime.Coordinate.Version)
            throw new ManagedAiException(AiFailureCode.RuntimeVersionMismatch);
    }

    private string ReleaseRoot(RuntimeState state)
    {
        OpenAiReleaseMetadataClient.ValidateVersion(state.Version);
        if (state.Target is not ("x86_64-pc-windows-msvc" or "aarch64-pc-windows-msvc") ||
            state.PackageSha256.Length != 64 || !state.PackageSha256.All(Uri.IsHexDigit)) throw Integrity();
        return paths.Under("runtimes", "openai-codex", "releases", state.Version + "-" + state.Target);
    }

    private ManagedRuntime Resolve(RuntimeState state)
    {
        var root = ReleaseRoot(state);
        SafeCodexArchive.ValidateLayout(root);
        return ToRuntime(state, root);
    }
    private ManagedRuntime ToRuntime(RuntimeState state, string root) => new(
        new(state.Version, state.Target, state.PackageSha256), root, Path.Combine(root, "bin", "codex.exe"), paths.Profile, state.ActivatedAtUtc);

    private static RuntimeState? ReadState(string path)
    {
        ManagedAiPaths.RejectReparsePoints(path);
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 4096) throw Integrity();
        try { return JsonSerializer.Deserialize(File.ReadAllText(path), RuntimeJsonContext.Default.RuntimeState) ?? throw Integrity(); }
        catch (JsonException) { throw Integrity(); }
    }

    private static void AtomicWrite(string path, RuntimeState state, string? backup = null)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(file, state, RuntimeJsonContext.Default.RuntimeState);
                file.Flush(flushToDisk: true);
            }
            if (File.Exists(path)) File.Replace(temp, path, backup);
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static void VerifyChecksumList(string text, ReleaseAsset package)
    {
        var matches = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.Length < 67) continue;
            var name = trimmed[64..].TrimStart(' ', '*');
            if (name == package.Name) matches.Add(trimmed[..64].ToLowerInvariant());
        }
        if (matches.Count != 1 || matches[0] != package.Sha256) throw Integrity();
    }
    private static ManagedAiException Integrity() => new(AiFailureCode.IntegrityMismatch);
}
