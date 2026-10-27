using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TableCloth.AppContainerPoc;

namespace TableCloth.ManagedAi.Windows;

// Discovery chooses only fixed entry points from enabled, app-owned installed packages.
// Native workers have no network capability. Public API access uses the bounded host transport.
public sealed class WindowsManagedAiSkillExecution(ManagedAiPaths paths,
    IManagedAiScriptExecutionRuntime runtimeManager, string hostExecutable,
    IManagedAiSkillMarketplace? marketplace = null) : IManagedAiSkillExecution
{
    public const string CalculatorSha256 = "279b627d4350b8895717a6ac3597a6e87cab7a9882004d639251ebbee3e7c947";
    public const string CalculatorId = "tablecloth-calculators";
    private IReadOnlyList<SkillCatalogEntry>? _catalog;

    public async Task<AiSkillExecutionBridge?> PrepareAsync(IReadOnlyList<AiSkill> skills,
        string runDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ManagedAiPaths.RejectReparsePoints(runDirectory);
        var packages = new List<(AiSkill Skill, IReadOnlyList<string> Files, IReadOnlyList<string> Scripts)>();
        foreach (var skill in skills.Where(x => x.Enabled && x.IsValid))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                ValidateId(skill.Id);
                var expected = paths.Under("profiles", "openai-codex", "codex-home", "skills", skill.Id);
                if (!Path.IsPathFullyQualified(skill.Directory) ||
                    !Path.GetFullPath(skill.Directory).Equals(expected, StringComparison.OrdinalIgnoreCase)) throw Blocked();
                if (!Directory.Exists(expected)) continue;
                // Do not impose execution-package byte limits on instruction-only documents.
                var scripts = DiscoverScripts(expected);
                if (scripts.Count == 0) continue;
                var files = EnumerateFiles(expected);
                if (scripts.Any(script => !files.Contains(script, StringComparer.Ordinal))) throw Blocked();
                var cost = checked(scripts.Count * files.Count);
                // The broker repeats each package inventory per tool. Bound that exact manifest
                // representation rather than advertising registrations the broker will reject.
                if (scripts.Count > 128 || cost > 4096) throw Blocked();
                packages.Add((skill, files, scripts));
            }
            catch (Exception error) when (error is ManagedAiException or IOException or UnauthorizedAccessException or ArgumentException or OverflowException)
            { /* This package contributes no tool; other skills and ordinary chat remain available. */ }
        }
        if (packages.Count == 0) return null;
        if (hostExecutable is null || !Path.IsPathFullyQualified(hostExecutable) ||
            !Path.GetFileName(hostExecutable).Equals("TableCloth.exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(hostExecutable))
            throw new ManagedAiException(AiFailureCode.SkillExecutionHostUnavailable);
        ManagedAiPaths.RejectReparsePoints(hostExecutable);
        string? manifest = null, snapshot = null;
        AiSkillExecutionBridge? bridge = null;
        try
        {
            var status = await runtimeManager.EnsureInstalledAsync(cancellationToken);
            if (!status.Python.Installed || !status.Node.Installed) throw new ManagedAiException(AiFailureCode.SkillIsolationFailed);
            var runtimeFiles = await runtimeManager.GetVerifiedFilesAsync(cancellationToken);
            var root = paths.Under("skill-execution");
            snapshot = paths.Under("skill-execution", "snapshots", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(snapshot);
            if (marketplace is not null && packages.Any(x => x.Skill.Source is { } source &&
                (_catalog is null || !_catalog.Any(entry => MatchesSource(entry, source)))))
            {
                try { _catalog = await marketplace.ListAsync(cancellationToken); }
                catch (ManagedAiException) { /* Offline script packages remain available. */ }
            }
            var registrations = new List<(string Name, string Kind, string Script, string Hash, string Root,
                IReadOnlyList<ScriptRuntimeFile> Files, string Description, string[] Hosts)>();
            var registeredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var packageIndex = 0;
            var manifestFileCount = 0;
            foreach (var (skill, files, scripts) in packages)
            {
                var package = Path.Combine(snapshot, (packageIndex++).ToString("x"));
                var originalCount = registrations.Count;
                try
                {
                    var cost = scripts.Count * files.Count;
                    if (registrations.Count + scripts.Count > 128 || manifestFileCount + cost > 4096) throw Blocked();
                    var names = scripts.Select(relative => ToolName(skill.Id, relative, scripts.Count == 1)).ToArray();
                    if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length || names.Any(registeredNames.Contains)) throw Blocked();
                    Directory.CreateDirectory(package);
                    var staged = new List<ScriptRuntimeFile>();
                    long total = 0;
                    foreach (var relative in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var source = Path.Combine(skill.Directory, relative);
                        ManagedAiPaths.RejectReparsePoints(source);
                        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
                        if (input.Length == 0 && scripts.Contains(relative, StringComparer.Ordinal)) throw Blocked();
                        if (input.Length > 1024 * 1024 || (total += input.Length) > 8 * 1024 * 1024) throw Blocked();
                        var hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(input, cancellationToken));
                        input.Position = 0;
                        var destination = Path.GetFullPath(Path.Combine(package, relative));
                        if (destination.Length > 240 || !destination.StartsWith(package + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw Blocked();
                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                        await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                            await input.CopyToAsync(output, cancellationToken);
                        staged.Add(new(destination, hash));
                    }
                    var hosts = NetworkHosts(skill);
                    for (var index = 0; index < scripts.Count; index++)
                    {
                        var relative = scripts[index];
                        var script = Path.Combine(package, relative);
                        var kind = Path.GetExtension(relative).Equals(".py", StringComparison.OrdinalIgnoreCase) ? "python-script" : "node-script";
                        var description = "Installed skill " + skill.Id + ", entry point " + relative.Replace('\\', '/') +
                            ". Pass script CLI arguments as arguments[] and optional text input as stdin. No host shell.";
                        registrations.Add((names[index], kind, script, staged.Single(x => x.Path == script).Sha256, package, staged, description, hosts));
                        registeredNames.Add(names[index]);
                    }
                    manifestFileCount += cost;
                }
                catch (Exception error) when (error is ManagedAiException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
                {
                    // A changed or incompatible installation cannot disable unrelated tools.
                    foreach (var registration in registrations.Skip(originalCount)) registeredNames.Remove(registration.Name);
                    registrations.RemoveRange(originalCount, registrations.Count - originalCount);
                    try { paths.DeleteOwnedDirectory(package); }
                    catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException or ManagedAiException) { }
                }
            }
            if (registrations.Count == 0) return null;
            manifest = paths.Under("skill-execution", "manifest-" + Guid.NewGuid().ToString("N") + ".json");
            using (var file = new FileStream(manifest, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new Utf8JsonWriter(file))
            {
                writer.WriteStartObject(); writer.WriteString("ownedRoot", root);
                writer.WriteString("pythonExecutable", runtimeManager.PythonExecutable);
                writer.WriteString("pythonSha256", runtimeFiles.Single(x => x.Path == runtimeManager.PythonExecutable).Sha256);
                writer.WriteString("nodeExecutable", runtimeManager.NodeExecutable);
                writer.WriteString("nodeSha256", runtimeFiles.Single(x => x.Path == runtimeManager.NodeExecutable).Sha256);
                writer.WritePropertyName("runtimeFiles"); WriteFiles(writer, runtimeFiles);
                writer.WriteStartArray("skills");
                foreach (var item in registrations)
                {
                    writer.WriteStartObject(); writer.WriteString("skillId", item.Name); writer.WriteString("kind", item.Kind);
                    writer.WriteString("scriptPath", item.Script); writer.WriteString("sha256", item.Hash);
                    writer.WriteString("packageRoot", item.Root); writer.WriteString("description", item.Description);
                    writer.WritePropertyName("packageFiles"); WriteFiles(writer, item.Files);
                    writer.WriteBoolean("requiresNetwork", item.Hosts.Length != 0);
                    writer.WriteStartArray("networkHosts"); foreach (var host in item.Hosts) writer.WriteStringValue(host); writer.WriteEndArray();
                    writer.WriteEndObject();
                }
                writer.WriteEndArray(); writer.WriteEndObject();
            }
            await SkillExecutionRegistry.LoadAsync(manifest, cancellationToken);
            bridge = new AiSkillExecutionBridge(hostExecutable, ["--skill-mcp", manifest], root,
                registrations.Select(x => x.Name).ToArray()) { SnapshotDirectory = snapshot };
            return bridge;
        }
        catch (OperationCanceledException) { throw; }
        catch (ManagedAiException) { throw; }
        catch { throw Blocked(); }
        finally
        {
            if (bridge is null)
            {
                if (manifest is not null) try { File.Delete(manifest); } catch (IOException) { }
                if (snapshot is not null) try { paths.DeleteOwnedDirectory(snapshot); } catch (IOException) { }
            }
        }
    }

    private string[] NetworkHosts(AiSkill skill)
    {
        if (skill.Source is not { } source || _catalog is null) return [];
        var entry = _catalog.SingleOrDefault(x => MatchesSource(x, source));
        // These are declarations, not Internet permission. The independent host bridge restricts
        // exact BOK sample and KOSIS GET routes, public DNS/IPs, credentials and request budgets.
        return entry?.Network == "required" ? ["ecos.bok.or.kr", "k-skill-proxy.nomadamas.org"] : [];
    }

    private static bool MatchesSource(SkillCatalogEntry entry, AiSkillSource source) =>
        entry.Id == source.CatalogId && entry.Repository == source.Repository && entry.SourcePath == source.SourcePath &&
        entry.SourceRef.Equals(source.SourceRef, StringComparison.OrdinalIgnoreCase);

    private static void WriteFiles(Utf8JsonWriter writer, IReadOnlyList<ScriptRuntimeFile> files)
    {
        writer.WriteStartArray();
        foreach (var file in files) { writer.WriteStartObject(); writer.WriteString("path", file.Path); writer.WriteString("sha256", file.Sha256); writer.WriteEndObject(); }
        writer.WriteEndArray();
    }

    private static IReadOnlyList<string> DiscoverScripts(string root)
    {
        var scripts = new List<string>();
        var pending = new Stack<string>(); pending.Push(root);
        var entries = 0;
        while (pending.TryPop(out var directory))
        {
            ManagedAiPaths.RejectReparsePoints(directory);
            foreach (var item in Directory.EnumerateFileSystemEntries(directory))
            {
                if (++entries > 1024) throw Blocked();
                var name = Path.GetFileName(item);
                if (ExcludedName(name)) continue;
                var attributes = File.GetAttributes(item);
                // Discovery never follows links. Full package validation below rejects them
                // whenever a real entry point makes this an executable package.
                if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (!NonEntryDirectory(name)) pending.Push(item);
                    continue;
                }
                var relative = Path.GetRelativePath(root, item);
                if (IsEntryPoint(relative) && new FileInfo(item).Length != 0)
                {
                    scripts.Add(relative);
                    if (scripts.Count > 128) throw Blocked();
                }
            }
        }
        return scripts.Order(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> EnumerateFiles(string root)
    {
        var result = new List<string>(); var pending = new Stack<string>(); pending.Push(root); long total = 0;
        while (pending.TryPop(out var directory))
        {
            ManagedAiPaths.RejectReparsePoints(directory);
            foreach (var item in Directory.EnumerateFileSystemEntries(directory))
            {
                ManagedAiPaths.RejectReparsePoints(item);
                var name = Path.GetFileName(item);
                if (ExcludedName(name)) continue;
                if (Directory.Exists(item)) { pending.Push(item); continue; }
                var info = new FileInfo(item);
                if (info.Length > 1024 * 1024 || (total += info.Length) > 8 * 1024 * 1024 || result.Count >= 256) throw Blocked();
                result.Add(Path.GetRelativePath(root, item));
            }
        }
        return result.Order(StringComparer.Ordinal).ToArray();
    }

    private static bool ExcludedName(string name) => name.StartsWith('.') ||
        name.Equals("__pycache__", StringComparison.OrdinalIgnoreCase) || name.Equals("secrets.env", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(name).ToLowerInvariant() is ".env" or ".pem" or ".pfx" or ".p12" or ".key";

    private static bool NonEntryDirectory(string name) => new[] { "tests", "test", "node_modules" }
        .Contains(name, StringComparer.OrdinalIgnoreCase);

    private static bool IsEntryPoint(string relative) =>
        !relative.Split(Path.DirectorySeparatorChar).Any(NonEntryDirectory) &&
        Path.GetExtension(relative).ToLowerInvariant() is ".py" or ".js" or ".mjs" or ".cjs";

    private static string ToolName(string id, string relative, bool single)
    {
        if (single && !id.Contains('.')) return id;
        var prefix = id.Replace('.', '-');
        return prefix[..Math.Min(prefix.Length, 48)] + "-" +
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(id + "\0" + relative)))[..12];
    }

    private static void ValidateId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 64 || !char.IsAsciiLetterOrDigit(id[0]) ||
            id.EndsWith('.') || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or '.'))) throw Blocked();
    }
    private static ManagedAiException Blocked() => new(AiFailureCode.BlockedByPolicy);
}
