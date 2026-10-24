using System.Text;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;

namespace TableCloth.ManagedAi.OpenAi;

internal sealed record ManagedSkillState(Dictionary<string, bool> Enabled);

[JsonSerializable(typeof(ManagedSkillState))]
[JsonSerializable(typeof(AiSkillSource))]
internal partial class ManagedSkillJsonContext : JsonSerializerContext;

public sealed class OpenAiCodexSkillManager(ManagedAiPaths paths, IManagedRuntimeManager runtimes,
    IManagedAiProfile profile, IJsonlProcessRunner runner) : IManagedAiSkillManager, IManagedAiCatalogSkillStore
{
    internal const string SourceMetadataFileName = ".tablecloth-source.json";
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
        try { return await DiscoverAsync(runtime, workingDirectory, allowInvalidSkills: true, cancellationToken); }
        finally { paths.DeleteOwnedDirectory(workingDirectory); }
    }

    public async Task<IReadOnlyList<AiSkill>> ImportAsync(string sourceDirectory, CancellationToken cancellationToken)
    {
        using var lease = paths.AcquireOperation();
        profile.Prepare();
        EnsureBundledSkills();
        var destination = await ImportCoreAsync(sourceDirectory, cancellationToken);
        try
        {
            var listed = await ListCoreAsync(cancellationToken);
            var importedId = Path.GetFileName(destination);
            if (listed.SingleOrDefault(x => string.Equals(x.Id, importedId, StringComparison.OrdinalIgnoreCase)) is not { IsValid: true })
                throw new ManagedAiException(AiFailureCode.SkillInvalid);
            return listed;
        }
        catch
        {
            paths.DeleteOwnedDirectory(destination);
            throw;
        }
    }

    public async Task<IReadOnlyList<AiSkill>> StoreCatalogAsync(string sourceDirectory, AiSkillSource source,
        AiSkillSource? expectedSource, CancellationToken cancellationToken)
    {
        ValidateSource(source);
        if (!string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(sourceDirectory)), source.CatalogId, StringComparison.OrdinalIgnoreCase))
            throw new ManagedAiException(AiFailureCode.SkillInvalid);
        using var lease = paths.AcquireOperation();
        profile.Prepare();
        EnsureBundledSkills();
        var destination = paths.Under("profiles", "openai-codex", "codex-home", "skills", source.CatalogId);
        var exists = Directory.Exists(destination) || File.Exists(destination);
        if (expectedSource is null && exists) throw new ManagedAiException(AiFailureCode.SkillAlreadyInstalled);
        if (expectedSource is not null)
        {
            ValidateSource(expectedSource);
            if (!Directory.Exists(destination) || LoadSource(destination, source.CatalogId) is not { } current ||
                !current.HasSameVersion(expectedSource) || !current.HasSameOrigin(source))
                throw new ManagedAiException(AiFailureCode.BlockedByPolicy);
            RejectNestedReparsePoints(destination);
        }
        // Refresh the current policy before touching files so rollback can restore its exact overlay.
        await ListCoreAsync(cancellationToken);
        var previousConfiguration = File.Exists(paths.SkillConfiguration) ? File.ReadAllText(paths.SkillConfiguration) : null;
        var staging = paths.Under("profiles", "openai-codex", "codex-home", "skills", ".replace-" + Guid.NewGuid().ToString("N"));
        var backup = paths.Under("profiles", "openai-codex", "codex-home", "skills", ".previous-" + Guid.NewGuid().ToString("N"));
        var installedNew = false;
        var committed = false;
        try
        {
            await CopyToStagingAsync(sourceDirectory, staging, cancellationToken);
            using (var metadata = new FileStream(Path.Combine(staging, SourceMetadataFileName), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(metadata, source, ManagedSkillJsonContext.Default.AiSkillSource);
                metadata.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (expectedSource is not null) Directory.Move(destination, backup);
            Directory.Move(staging, destination);
            installedNew = true;
            var listed = await ListCoreAsync(cancellationToken);
            if (listed.SingleOrDefault(x => string.Equals(x.Id, source.CatalogId, StringComparison.OrdinalIgnoreCase)) is not { IsValid: true })
                throw new ManagedAiException(AiFailureCode.SkillInvalid);
            cancellationToken.ThrowIfCancellationRequested();
            committed = true;
            return listed;
        }
        catch
        {
            if (installedNew) paths.DeleteOwnedDirectory(destination);
            if (Directory.Exists(backup)) Directory.Move(backup, destination);
            if (previousConfiguration is not null) AtomicWrite(paths.SkillConfiguration, previousConfiguration);
            throw;
        }
        finally
        {
            SkillDirectoryCleanup.DeleteOwnedBestEffort(paths, staging);
            if (committed) SkillDirectoryCleanup.DeleteOwnedBestEffort(paths, backup);
        }
    }

    public async Task<IReadOnlyList<AiSkill>> SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken)
    {
        ValidateId(id);
        using var lease = paths.AcquireOperation();
        profile.Prepare();
        EnsureBundledSkills();
        if (!File.Exists(Path.Combine(paths.Skills, id, "SKILL.md"))) throw new ManagedAiException(AiFailureCode.SkillInvalid);
        if (enabled && (await ListCoreAsync(cancellationToken)).SingleOrDefault(x =>
            string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)) is not { IsValid: true })
            throw new ManagedAiException(AiFailureCode.SkillInvalid);
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
                !state.Enabled.TryGetValue(id, out var enabled) || enabled) { Source = LoadSource(directory, id) });
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
    public Task<IReadOnlyList<AiSkill>> SynchronizeAsync(ManagedRuntime runtime, string workingDirectory,
        CancellationToken cancellationToken) => DiscoverAsync(runtime, workingDirectory, allowInvalidSkills: false, cancellationToken);

    private async Task<IReadOnlyList<AiSkill>> DiscoverAsync(ManagedRuntime runtime, string workingDirectory,
        bool allowInvalidSkills, CancellationToken cancellationToken)
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
        var invalid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var error in catalog.Errors)
        {
            if (!TryManagedId(error.Path, out var id)) continue;
            if (!allowInvalidSkills) throw new ManagedAiException(AiFailureCode.SkillInvalid);
            invalid[id] = Path.Combine(paths.Skills, id, "SKILL.md");
        }

        foreach (var skill in catalog.Skills)
        {
            if (!TryManagedId(skill.Path, out var id) || invalid.ContainsKey(id)) continue;
            var enabled = !state.Enabled.TryGetValue(id, out var configured) || configured;
            var directory = Path.GetDirectoryName(skill.Path)!;
            managed.Add(new(id, skill.Name, skill.Description, directory, enabled) { Source = LoadSource(directory, id) });
        }
        foreach (var id in invalid.Keys)
        {
            var enabled = !state.Enabled.TryGetValue(id, out var configured) || configured;
            managed.Add(new(id, id, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko"
                ? "SKILL.md를 읽을 수 없습니다. 파일을 수정하거나 스킬을 제거할 수 있습니다."
                : "Could not read SKILL.md. You can edit the file or remove this skill.", Path.Combine(paths.Skills, id), enabled)
                { IsValid = false, Source = LoadSource(Path.Combine(paths.Skills, id), id) });
        }
        WriteSkillConfiguration(catalog.Skills, state, invalid.Values);
        return managed.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private void EnsureBundledSkills()
    {
        EnsureBundledSkill("tablecloth-certificate-expiry", "certificate-expiry-skill.installed",
            "TableCloth.ManagedAi.OpenAi.Bundled.CertificateExpirySkill");
        UpgradeBundledCertificateSkill();
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

    private void UpgradeBundledCertificateSkill()
    {
        const string priorLfHash = "C08D90D73961A2134E684641D7A40638A94FC584B5555D854F4B8FB4D81E3182";
        const string priorCrlfHash = "0ABDAE1744AF5F66E45B39FB2FE829EE13869BA37062FB9C7ECC958D1CC118FE";
        var manifest = Path.Combine(paths.Skills, "tablecloth-certificate-expiry", "SKILL.md");
        if (!File.Exists(manifest)) return;
        ManagedAiPaths.RejectReparsePoints(manifest);
        var currentHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifest)));
        if (currentHash is not (priorLfHash or priorCrlfHash)) return;
        using var source = typeof(OpenAiCodexSkillManager).Assembly.GetManifestResourceStream(
            "TableCloth.ManagedAi.OpenAi.Bundled.CertificateExpirySkill")
            ?? throw new ManagedAiException(AiFailureCode.SkillInvalid);
        var staging = manifest + ".update-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var target = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                source.CopyTo(target);
            File.Move(staging, manifest, overwrite: true);
        }
        finally { if (File.Exists(staging)) File.Delete(staging); }
    }

    private async Task<string> ImportCoreAsync(string sourceDirectory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory) || !Path.IsPathFullyQualified(sourceDirectory) ||
            sourceDirectory.StartsWith("\\\\", StringComparison.Ordinal)) throw new ManagedAiException(AiFailureCode.SkillInvalid);
        var id = Path.GetFileName(Path.TrimEndingDirectorySeparator(sourceDirectory));
        ValidateId(id);
        var destination = paths.Under("profiles", "openai-codex", "codex-home", "skills", id);
        if (Directory.Exists(destination) || File.Exists(destination)) throw new ManagedAiException(AiFailureCode.SkillAlreadyInstalled);
        var staging = paths.Under("profiles", "openai-codex", "codex-home", "skills", ".import-" + Guid.NewGuid().ToString("N"));
        try
        {
            await CopyToStagingAsync(sourceDirectory, staging, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(staging, destination);
            return destination;
        }
        finally { SkillDirectoryCleanup.DeleteOwnedBestEffort(paths, staging); }
    }

    private static async Task CopyToStagingAsync(string sourceDirectory, string staging, CancellationToken cancellationToken)
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

        if (files.Any(x => string.Equals(x.DirectoryName, source, StringComparison.OrdinalIgnoreCase) &&
            x.Name.Equals(SourceMetadataFileName, StringComparison.OrdinalIgnoreCase))) throw new ManagedAiException(AiFailureCode.SkillInvalid);
        Directory.CreateDirectory(staging);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(source, file.FullName);
            var target = Path.GetFullPath(Path.Combine(staging, relative));
            if (target.Length > 240 || !target.StartsWith(staging + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ManagedAiException(AiFailureCode.SkillInvalid);
            ManagedAiPaths.RejectReparsePoints(target);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var input = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await input.CopyToAsync(output, cancellationToken);
        }
    }

    private static void ValidateSource(AiSkillSource source)
    {
        SkillCatalogValidation.ValidateId(source.CatalogId);
        SkillCatalogValidation.Repository(source.Repository);
        SkillCatalogValidation.Path(source.SourcePath, allowRoot: true);
        if (!SkillCatalogValidation.IsSha(source.SourceRef)) throw new ManagedAiException(AiFailureCode.SkillInvalid);
    }

    private static AiSkillSource? LoadSource(string directory, string id)
    {
        var path = Path.Combine(directory, SourceMetadataFileName);
        ManagedAiPaths.RejectReparsePoints(path);
        if (!File.Exists(path) || new FileInfo(path).Length > 8192) return null;
        try
        {
            var source = JsonSerializer.Deserialize(File.ReadAllText(path), ManagedSkillJsonContext.Default.AiSkillSource);
            if (source is null || !string.Equals(source.CatalogId, id, StringComparison.OrdinalIgnoreCase)) return null;
            ValidateSource(source);
            return source;
        }
        catch (Exception ex) when (ex is JsonException or ManagedAiException or ArgumentException or NullReferenceException)
        { return null; }
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

    private void WriteSkillConfiguration(IReadOnlyList<CodexSkillMetadata> discovered, ManagedSkillState state,
        IEnumerable<string>? invalidManifests = null)
    {
        var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (invalidManifests is not null) disabled.UnionWith(invalidManifests);
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
