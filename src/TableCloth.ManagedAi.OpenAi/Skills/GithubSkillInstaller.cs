using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TableCloth.ManagedAi.OpenAi;

public sealed class GithubSkillInstaller(IHttpClientFactory clients, ManagedAiPaths paths,
    IManagedAiCatalogSkillStore skills)
{
    private const int MaxMetadataBytes = 2 * 1024 * 1024;
    private const int MaxTotalMetadataBytes = 16 * 1024 * 1024;
    private const int MaxRequests = 256;
    private const int MaxFileBytes = 1024 * 1024;
    private const int MaxTotalBytes = 8 * 1024 * 1024;
    private const int MaxFiles = 128;
    private const int MaxDirectories = 64;

    public async Task<IReadOnlyList<AiSkill>> InstallAsync(SkillCatalogEntry entry, CancellationToken cancellationToken)
        => await DownloadAndStoreAsync(entry, expectedSource: null, cancellationToken);

    public async Task<IReadOnlyList<AiSkill>> UpdateAsync(SkillCatalogEntry entry, AiSkillSource expectedSource,
        CancellationToken cancellationToken) => await DownloadAndStoreAsync(entry, expectedSource, cancellationToken);

    private async Task<IReadOnlyList<AiSkill>> DownloadAndStoreAsync(SkillCatalogEntry entry, AiSkillSource? expectedSource,
        CancellationToken cancellationToken)
    {
        if (!entry.CanInstall) throw new ManagedAiException(AiFailureCode.BlockedByPolicy);
        SkillCatalogValidation.ValidateId(entry.Id);
        SkillCatalogValidation.Path(entry.SourcePath, allowRoot: true);
        var (owner, repository) = SkillCatalogValidation.Repository(entry.Repository);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        var budget = new DownloadBudget();
        // The skill manager takes its own operation lease when committing. Downloads do not hold that lease.
        var staging = paths.Under("cache", "marketplace", Guid.NewGuid().ToString("N"));
        var source = paths.Under("cache", "marketplace", Path.GetFileName(staging), entry.Id);
        try
        {
            Directory.CreateDirectory(source);
            var api = $"https://api.github.com/repos/{owner}/{repository}";
            var treeSha = await GetCommitTreeAsync(api, entry.SourceRef, budget, timeout.Token);
            var rootTreeSha = treeSha;
            // GitHub's Contents API follows some symlinks. Tree modes establish that every source
            // directory and file really belongs to this immutable commit before content is requested.
            if (entry.SourcePath != ".")
                foreach (var segment in entry.SourcePath.Split('/'))
                {
                    var tree = await GetTreeAsync(api, treeSha, budget, timeout.Token);
                    var child = tree.SingleOrDefault(x => x.Path == segment);
                    if (child is null || child.Type != "tree" || child.Mode != "040000") throw Invalid();
                    treeSha = child.Sha;
                }
            var files = new List<RemoteFile>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<(string Sha, string Prefix)>();
            pending.Push((treeSha, string.Empty));
            var directories = 0;
            long total = 0;
            while (pending.TryPop(out var directory))
            {
                if (++directories > MaxDirectories) throw Invalid();
                foreach (var child in await GetTreeAsync(api, directory.Sha, budget, timeout.Token))
                {
                    var relative = directory.Prefix + child.Path;
                    SkillCatalogValidation.Path(relative);
                    if (!names.Add(relative)) throw Invalid();
                    if (child.Type == "tree" && child.Mode == "040000")
                    {
                        pending.Push((child.Sha, relative + "/"));
                        if (pending.Count + directories > MaxDirectories) throw Invalid();
                    }
                    else if (child.Type == "blob" && child.Mode is "100644" or "100755")
                    {
                        if (child.Size is < 0 or > MaxFileBytes || (total += child.Size) > MaxTotalBytes || files.Count >= MaxFiles)
                            throw Invalid();
                        files.Add(new(relative, child.Sha, child.Size));
                    }
                    else throw Invalid(); // symlinks, submodules and unknown Git modes
                }
            }
            if (!files.Any(x => x.Path == "SKILL.md") ||
                files.Any(x => x.Path != "SKILL.md" && System.IO.Path.GetFileName(x.Path).Equals("SKILL.md", StringComparison.OrdinalIgnoreCase)))
                throw Invalid();
            if (names.Contains("LICENSE.upstream") || names.Contains(OpenAiCodexSkillManager.SourceMetadataFileName) || files.Count >= MaxFiles)
                throw Invalid();
            var licensePath = entry.LicenseSourcePath ?? throw Invalid();
            SkillCatalogValidation.Path(licensePath);
            var license = await GetPinnedFileAsync(api, rootTreeSha, licensePath, budget, timeout.Token);
            if (total + license.Size > MaxTotalBytes) throw Invalid();
            foreach (var file in files)
            {
                timeout.Token.ThrowIfCancellationRequested();
                var repositoryPath = entry.SourcePath == "." ? file.Path : entry.SourcePath + "/" + file.Path;
                var content = await GetContentAsync(api, repositoryPath, entry.SourceRef, file, budget, timeout.Token);
                var target = paths.Under("cache", "marketplace", Path.GetFileName(staging), entry.Id,
                    file.Path.Replace('/', System.IO.Path.DirectorySeparatorChar));
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
                ManagedAiPaths.RejectReparsePoints(target);
                await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await output.WriteAsync(content, timeout.Token);
            }
            var licenseContent = await GetContentAsync(api, licensePath, entry.SourceRef, license, budget, timeout.Token);
            var licenseTarget = paths.Under("cache", "marketplace", Path.GetFileName(staging), entry.Id, "LICENSE.upstream");
            await using (var output = new FileStream(licenseTarget, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await output.WriteAsync(licenseContent, timeout.Token);
            return await skills.StoreCatalogAsync(source,
                new(entry.Id, entry.Repository, entry.SourcePath, entry.SourceRef), expectedSource, timeout.Token);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or
            FormatException or ArgumentException)
        { throw Invalid(); }
        catch (Exception ex) when (ex is HttpRequestException or IOException ||
            ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        { throw new ManagedAiException(AiFailureCode.CatalogUnavailable); }
        finally { SkillDirectoryCleanup.DeleteOwnedBestEffort(paths, staging); }
    }

    private async Task<string> GetCommitTreeAsync(string api, string commit, DownloadBudget budget, CancellationToken cancellationToken)
    {
        using var json = await GetJsonAsync(api + "/git/commits/" + commit, budget, cancellationToken);
        var root = json.RootElement;
        if (!string.Equals(String(root, "sha"), commit, StringComparison.OrdinalIgnoreCase)) throw Invalid();
        return Sha(root.GetProperty("tree"));
    }

    private async Task<IReadOnlyList<TreeEntry>> GetTreeAsync(string api, string sha, DownloadBudget budget, CancellationToken cancellationToken)
    {
        using var json = await GetJsonAsync(api + "/git/trees/" + sha, budget, cancellationToken);
        var root = json.RootElement;
        if (!string.Equals(Sha(root), sha, StringComparison.OrdinalIgnoreCase) || root.GetProperty("truncated").GetBoolean()) throw Invalid();
        var tree = root.GetProperty("tree");
        if (tree.ValueKind != JsonValueKind.Array || tree.GetArrayLength() > 2000) throw Invalid();
        var result = new List<TreeEntry>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in tree.EnumerateArray())
        {
            var path = String(item, "path");
            SkillCatalogValidation.Path(path);
            if (path.Contains('/') || !names.Add(path)) throw Invalid();
            var type = String(item, "type");
            var size = type == "blob" ? item.GetProperty("size").GetInt64() : 0;
            result.Add(new(path, String(item, "mode"), type, Sha(item), size));
        }
        return result;
    }

    private async Task<RemoteFile> GetPinnedFileAsync(string api, string treeSha, string path, DownloadBudget budget, CancellationToken cancellationToken)
    {
        var segments = path.Split('/');
        for (var index = 0; index < segments.Length; index++)
        {
            var entry = (await GetTreeAsync(api, treeSha, budget, cancellationToken)).SingleOrDefault(x => x.Path == segments[index]);
            if (entry is null) throw Invalid();
            if (index == segments.Length - 1)
            {
                if (entry.Type != "blob" || entry.Mode is not "100644" and not "100755" || entry.Size is <= 0 or > MaxFileBytes)
                    throw Invalid();
                return new(path, entry.Sha, entry.Size);
            }
            if (entry.Type != "tree" || entry.Mode != "040000") throw Invalid();
            treeSha = entry.Sha;
        }
        throw Invalid();
    }

    private async Task<byte[]> GetContentAsync(string api, string path, string sourceRef, RemoteFile file,
        DownloadBudget budget, CancellationToken cancellationToken)
    {
        var encoded = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
        using var json = await GetJsonAsync(api + "/contents/" + encoded + "?ref=" + sourceRef, budget, cancellationToken);
        var root = json.RootElement;
        if (String(root, "type") != "file" || String(root, "encoding") != "base64" || String(root, "path") != path ||
            !string.Equals(Sha(root), file.Sha, StringComparison.OrdinalIgnoreCase) || root.GetProperty("size").GetInt64() != file.Size ||
            root.TryGetProperty("submodule_git_url", out var submodule) && submodule.ValueKind != JsonValueKind.Null) throw Invalid();
        var bytes = Convert.FromBase64String(String(root, "content"));
        if (bytes.Length != file.Size || bytes.Length > MaxFileBytes || IsLfsPointer(bytes)) throw Invalid();
        // Git blob identity binds the content response to the previously validated tree entry.
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
        hash.AppendData(Encoding.ASCII.GetBytes("blob " + bytes.Length.ToString(CultureInfo.InvariantCulture) + "\0"));
        hash.AppendData(bytes);
        if (!Convert.ToHexString(hash.GetHashAndReset()).Equals(file.Sha, StringComparison.OrdinalIgnoreCase)) throw Invalid();
        return bytes;
    }

    private async Task<JsonDocument> GetJsonAsync(string url, DownloadBudget budget, CancellationToken cancellationToken)
    {
        if (++budget.Requests > MaxRequests || budget.Bytes >= MaxTotalMetadataBytes)
            throw new ManagedAiException(AiFailureCode.OutputLimitExceeded);
        var bytes = await SkillMarketplaceHttp.GetAsync(clients, new(url),
            Math.Min(MaxMetadataBytes, MaxTotalMetadataBytes - budget.Bytes), cancellationToken);
        budget.Bytes += bytes.Length;
        return JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
    }

    private static bool IsLfsPointer(byte[] content) =>
        content.AsSpan().StartsWith("version https://git-lfs.github.com/spec/v1"u8);

    private static string String(JsonElement element, string name) => element.GetProperty(name).GetString() ?? throw Invalid();
    private static string Sha(JsonElement element)
    {
        var sha = String(element, "sha");
        return SkillCatalogValidation.IsSha(sha) ? sha : throw Invalid();
    }
    private static ManagedAiException Invalid() => new(AiFailureCode.SkillInvalid);
    private sealed class DownloadBudget { public int Requests; public int Bytes; }
    private sealed record TreeEntry(string Path, string Mode, string Type, string Sha, long Size);
    private sealed record RemoteFile(string Path, string Sha, long Size);
}

public sealed class ManagedAiSkillMarketplace(SkillCatalogClient catalog, GithubSkillInstaller installer,
    IManagedAiSkillManager skills) : IManagedAiSkillMarketplace
{
    public Task<IReadOnlyList<SkillCatalogEntry>> ListAsync(CancellationToken cancellationToken) => catalog.ListAsync(cancellationToken);

    public async Task<IReadOnlyList<AiSkill>> InstallAsync(string id, CancellationToken cancellationToken)
    {
        SkillCatalogValidation.ValidateId(id);
        // Re-read the catalog instead of trusting a stale row or a caller-supplied source URL.
        var entry = (await catalog.ListAsync(cancellationToken)).SingleOrDefault(x => x.Id == id);
        if (entry is null || !entry.CanInstall) throw new ManagedAiException(AiFailureCode.BlockedByPolicy);
        if ((await skills.ListAsync(cancellationToken)).Any(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)))
            throw new ManagedAiException(AiFailureCode.SkillAlreadyInstalled);
        return await installer.InstallAsync(entry, cancellationToken);
    }

    public async Task<IReadOnlyList<AiSkill>> UpdateAsync(string id, CancellationToken cancellationToken)
    {
        SkillCatalogValidation.ValidateId(id);
        var entry = (await catalog.ListAsync(cancellationToken)).SingleOrDefault(x => x.Id == id);
        if (entry is null || !entry.CanInstall) throw new ManagedAiException(AiFailureCode.BlockedByPolicy);
        var installed = await skills.ListAsync(cancellationToken);
        var current = installed.SingleOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
        var source = new AiSkillSource(entry.Id, entry.Repository, entry.SourcePath, entry.SourceRef);
        if (current?.Source is not { } provenance || !provenance.HasSameOrigin(source))
            throw new ManagedAiException(AiFailureCode.BlockedByPolicy);
        if (provenance.HasSameVersion(source)) return installed;
        return await installer.UpdateAsync(entry, provenance, cancellationToken);
    }
}
