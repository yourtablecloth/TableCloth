using System.Text;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TableCloth.ManagedAi.OpenAi;

internal sealed record ManagedSkillState(Dictionary<string, bool> Enabled);

[JsonSerializable(typeof(ManagedSkillState))]
internal partial class ManagedSkillJsonContext : JsonSerializerContext;

public sealed class OpenAiCodexSkillManager(ManagedAiPaths paths, IManagedRuntimeManager runtimes,
    IManagedAiProfile profile, IJsonlProcessRunner runner) : IManagedAiSkillManager
{
    private const int MaxFiles = 500;
    private const long MaxFileBytes = 25L * 1024 * 1024;
    private const long MaxTotalBytes = 50L * 1024 * 1024;

    public string StorageDirectory => paths.Skills;

    public async Task<IReadOnlyList<AiSkill>> ListAsync(CancellationToken cancellationToken)
    {
        using var lease = paths.AcquireOperation();
        profile.Prepare();
        EnsureBundledSkills();
        return await ListCoreAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<AiSkill>> ListCoreAsync(CancellationToken cancellationToken)
    {
        var runtime = await runtimes.GetActiveAsync(cancellationToken);
        if (runtime is null) return ListStoredSkills();
        var workingDirectory = paths.Under("runs", "skills-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        try { return await SynchronizeAsync(runtime, workingDirectory, cancellationToken); }
        finally { paths.DeleteOwnedDirectory(workingDirectory); }
    }

    public async Task<IReadOnlyList<AiSkill>> ImportAsync(string sourceDirectory, CancellationToken cancellationToken)
    {
        using var lease = paths.AcquireOperation();
        profile.Prepare();
        EnsureBundledSkills();
        var destination = await ImportCoreAsync(sourceDirectory, cancellationToken);
        try { return await ListCoreAsync(cancellationToken); }
        catch (ManagedAiException ex) when (ex.Code == AiFailureCode.SkillInvalid)
        {
            paths.DeleteOwnedDirectory(destination);
            throw;
        }
    }

    public async Task<IReadOnlyList<AiSkill>> SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken)
    {
        ValidateId(id);
        using var lease = paths.AcquireOperation();
        profile.Prepare();
        EnsureBundledSkills();
        if (!File.Exists(Path.Combine(paths.Skills, id, "SKILL.md"))) throw new ManagedAiException(AiFailureCode.SkillInvalid);
        var state = LoadState();
        state.Enabled[id] = enabled;
        SaveState(state);
        return await ListCoreAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AiSkill>> RemoveAsync(string id, CancellationToken cancellationToken)
    {
        ValidateId(id);
        using var lease = paths.AcquireOperation();
        profile.Prepare();
        EnsureBundledSkills();
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.Combine(paths.Skills, id);
        if (!Directory.Exists(directory) || !File.Exists(Path.Combine(directory, "SKILL.md")))
            throw new ManagedAiException(AiFailureCode.SkillInvalid);
        var state = LoadState();
        RejectNestedReparsePoints(directory);
        paths.DeleteOwnedDirectory(directory);
        if (state.Enabled.Remove(id)) SaveState(state);
        return await ListCoreAsync(cancellationToken);
    }

    private IReadOnlyList<AiSkill> ListStoredSkills()
    {
        var state = LoadState();
        if (!Directory.Exists(paths.Skills)) return [];
        var result = new List<AiSkill>();
        foreach (var directory in Directory.EnumerateDirectories(paths.Skills))
        {
            var id = Path.GetFileName(directory);
            if (id.StartsWith(".", StringComparison.Ordinal)) continue;
            try { ValidateId(id); }
            catch (ManagedAiException) { continue; }
            ManagedAiPaths.RejectReparsePoints(directory);
            var manifest = Path.Combine(directory, "SKILL.md");
            if (!File.Exists(manifest)) continue;
            ManagedAiPaths.RejectReparsePoints(manifest);
            result.Add(new(id, id, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko"
                ? "런타임 설치 후 스킬 설명을 확인할 수 있습니다."
                : "Install the runtime to view this skill's description.", directory,
                !state.Enabled.TryGetValue(id, out var enabled) || enabled));
        }
        return result.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private static void RejectNestedReparsePoints(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new ManagedAiException(AiFailureCode.SkillInvalid);
            foreach (var child in Directory.EnumerateDirectories(directory)) pending.Push(child);
            foreach (var file in Directory.EnumerateFiles(directory))
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new ManagedAiException(AiFailureCode.SkillInvalid);
        }
    }

    // The caller holds the managed runtime operation lease so discovery and the following model invocation are serialized.
    public async Task<IReadOnlyList<AiSkill>> SynchronizeAsync(ManagedRuntime runtime, string workingDirectory,
        CancellationToken cancellationToken)
    {
        EnsureBundledSkills();
        Directory.CreateDirectory(paths.Skills);
        var exchange = new CodexSkillListExchange();
        var spec = OpenAiCodexInvocationBuilder.Command(runtime, workingDirectory,
            ["--strict-config", "app-server", "--listen", "stdio://"], TimeSpan.FromSeconds(30)) with
        {
            Input = CodexSkillListExchange.Initialize,
            Respond = exchange.Accept,
            MaxStdoutBytes = 4 * 1024 * 1024,
            MaxLineBytes = 1024 * 1024
        };
        var outcome = await runner.RunAsync(spec, _ => { }, null, cancellationToken);
        var catalog = exchange.Complete(outcome.ExitCode);
        var state = LoadState();
        var managed = new List<AiSkill>();

        foreach (var error in catalog.Errors)
            if (TryManagedId(error.Path, out _)) throw new ManagedAiException(AiFailureCode.SkillInvalid);

        foreach (var skill in catalog.Skills)
        {
            if (!TryManagedId(skill.Path, out var id)) continue;
            var enabled = !state.Enabled.TryGetValue(id, out var configured) || configured;
            managed.Add(new(id, skill.Name, skill.Description, Path.GetDirectoryName(skill.Path)!, enabled));
        }
        WriteSkillConfiguration(catalog.Skills, state);
        return managed.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private void EnsureBundledSkills()
    {
        EnsureBundledSkill("tablecloth-certificate-expiry", "certificate-expiry-skill.installed",
            "TableCloth.ManagedAi.OpenAi.Bundled.CertificateExpirySkill");
        EnsureBundledSkill("tablecloth-windows-sandbox", "windows-sandbox-skill.installed",
            "TableCloth.ManagedAi.OpenAi.Bundled.WindowsSandboxSkill");
    }

    private void EnsureBundledSkill(string id, string markerName, string resourceName)
    {
        var marker = paths.Under("profiles", "openai-codex", markerName);
        if (File.Exists(marker)) return;
        var destination = Path.Combine(paths.Skills, id);
        ManagedAiPaths.RejectReparsePoints(destination);
        if (!Directory.Exists(destination))
            Directory.CreateDirectory(destination);
        var manifest = Path.Combine(destination, "SKILL.md");
        if (!File.Exists(manifest))
        {
            using var source = typeof(OpenAiCodexSkillManager).Assembly.GetManifestResourceStream(resourceName)
                ?? throw new ManagedAiException(AiFailureCode.SkillInvalid);
            using var target = new FileStream(manifest, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            source.CopyTo(target);
        }
        File.WriteAllText(marker, "1", new UTF8Encoding(false));
    }

    private async Task<string> ImportCoreAsync(string sourceDirectory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory) || !Path.IsPathFullyQualified(sourceDirectory) ||
            sourceDirectory.StartsWith("\\\\", StringComparison.Ordinal)) throw new ManagedAiException(AiFailureCode.SkillInvalid);
        var source = Path.GetFullPath(sourceDirectory).TrimEnd(Path.DirectorySeparatorChar);
        ManagedAiPaths.RejectReparsePoints(source);
        if (!Directory.Exists(source)) throw new ManagedAiException(AiFailureCode.SkillInvalid);
        var id = new DirectoryInfo(source).Name;
        ValidateId(id);
        var files = EnumerateSafeFiles(source);
        var manifests = files.Where(x => string.Equals(x.Name, "SKILL.md", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (manifests.Length != 1 || !string.Equals(manifests[0].DirectoryName, source, StringComparison.OrdinalIgnoreCase))
            throw new ManagedAiException(AiFailureCode.SkillInvalid);

        var destination = Path.Combine(paths.Skills, id);
        if (Directory.Exists(destination) || File.Exists(destination)) throw new ManagedAiException(AiFailureCode.SkillAlreadyInstalled);
        var staging = Path.Combine(paths.Skills, ".import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(source, file.FullName);
                var target = Path.GetFullPath(Path.Combine(staging, relative));
                if (!target.StartsWith(staging + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new ManagedAiException(AiFailureCode.SkillInvalid);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await using var input = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await input.CopyToAsync(output, cancellationToken);
            }
            Directory.Move(staging, destination);
            return destination;
        }
        finally { paths.DeleteOwnedDirectory(staging); }
    }

    private static IReadOnlyList<FileInfo> EnumerateSafeFiles(string source)
    {
        var files = new List<FileInfo>();
        long total = 0;
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new(source));
        while (pending.TryPop(out var directory))
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new ManagedAiException(AiFailureCode.SkillInvalid);
            foreach (var child in directory.EnumerateDirectories()) pending.Push(child);
            foreach (var file in directory.EnumerateFiles())
            {
                if ((file.Attributes & FileAttributes.ReparsePoint) != 0 || file.Length > MaxFileBytes)
                    throw new ManagedAiException(AiFailureCode.SkillInvalid);
                total += file.Length;
                files.Add(file);
                if (files.Count > MaxFiles || total > MaxTotalBytes) throw new ManagedAiException(AiFailureCode.SkillInvalid);
            }
        }
        return files;
    }

    private void WriteSkillConfiguration(IReadOnlyList<CodexSkillMetadata> discovered, ManagedSkillState state)
    {
        var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var skill in discovered)
        {
            if (TryManagedId(skill.Path, out var id))
            {
                if (state.Enabled.TryGetValue(id, out var enabled) && !enabled) disabled.Add(skill.Path);
            }
            else if (!IsManagedSystemSkill(skill.Path)) disabled.Add(skill.Path);
        }

        var text = new StringBuilder("# Generated by TableCloth. External Codex skills are disabled for this backend.\n");
        foreach (var path in disabled.Order(StringComparer.OrdinalIgnoreCase))
        {
            text.Append("\n[[skills.config]]\npath = \"").Append(EscapeToml(path)).Append("\"\nenabled = false\n");
        }
        AtomicWrite(paths.SkillConfiguration, text.ToString());
    }

    private bool TryManagedId(string manifestPath, out string id)
    {
        id = string.Empty;
        var directory = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
        if (directory is null) return false;
        var relative = Path.GetRelativePath(paths.Skills, directory);
        if (relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || relative == ".." ||
            relative.StartsWith(".", StringComparison.Ordinal) || relative.Contains(Path.DirectorySeparatorChar)) return false;
        try { ValidateId(relative); }
        catch (ManagedAiException) { return false; }
        ManagedAiPaths.RejectReparsePoints(manifestPath);
        id = relative;
        return string.Equals(Path.GetFileName(manifestPath), "SKILL.md", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsManagedSystemSkill(string manifestPath)
    {
        var systemRoot = Path.Combine(paths.Skills, ".system") + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(manifestPath).StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase)) return false;
        ManagedAiPaths.RejectReparsePoints(manifestPath);
        return true;
    }

    private ManagedSkillState LoadState()
    {
        ManagedAiPaths.RejectReparsePoints(paths.SkillState);
        if (!File.Exists(paths.SkillState)) return new(new(StringComparer.OrdinalIgnoreCase));
        if (new FileInfo(paths.SkillState).Length > 65536) throw new ManagedAiException(AiFailureCode.SkillIsolationFailed);
        try
        {
            var state = JsonSerializer.Deserialize(File.ReadAllText(paths.SkillState), ManagedSkillJsonContext.Default.ManagedSkillState)
                ?? throw new ManagedAiException(AiFailureCode.SkillIsolationFailed);
            if (state.Enabled.Count > 1024) throw new ManagedAiException(AiFailureCode.SkillIsolationFailed);
            foreach (var id in state.Enabled.Keys) ValidateId(id);
            return new(new(state.Enabled, StringComparer.OrdinalIgnoreCase));
        }
        catch (JsonException) { throw new ManagedAiException(AiFailureCode.SkillIsolationFailed); }
    }

    private void SaveState(ManagedSkillState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(paths.SkillState)!);
        var temp = paths.SkillState + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(file, state, ManagedSkillJsonContext.Default.ManagedSkillState);
                file.Flush(flushToDisk: true);
            }
            if (File.Exists(paths.SkillState)) File.Replace(temp, paths.SkillState, null);
            else File.Move(temp, paths.SkillState);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static void AtomicWrite(string path, string content)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static string EscapeToml(string value)
    {
        if (value.Any(char.IsControl) || value.Length > 1024) throw new ManagedAiException(AiFailureCode.SkillIsolationFailed);
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static void ValidateId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 64 || id[0] == '.' || !char.IsAsciiLetterOrDigit(id[0]) ||
            id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
            throw new ManagedAiException(AiFailureCode.SkillInvalid);
    }
}
