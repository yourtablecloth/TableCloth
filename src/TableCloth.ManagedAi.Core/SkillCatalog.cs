namespace TableCloth.ManagedAi;

public sealed record SkillCatalogEntry(string Id, string Category, string DisplayName, string Description,
    string Repository, string SourcePath, string SourceRef, string Author, string LicenseSpdx,
    string InstallMode, string Network, string Notes)
{
    public string AuthorUrl { get; init; } = string.Empty;
    public string LicenseSource { get; init; } = string.Empty;
    public string Runtime { get; init; } = "Codex";

    public bool CanInstall => InstallMode == "upstream" && Runtime == "Codex" &&
        SourceRef.Length == 40 && SourceRef.All(char.IsAsciiHexDigit) && LicenseSourcePath is not null;

    public string? LicenseSourcePath
    {
        get
        {
            var prefix = Repository + "/blob/" + SourceRef + "/";
            if (!LicenseSource.StartsWith(prefix, StringComparison.Ordinal)) return null;
            var path = LicenseSource[prefix.Length..];
            if (path.Length is 0 or > 160 || path.Split('/').Any(x => x.Length is 0 or > 100 ||
                x is "." or ".." || x.EndsWith('.') || IsDeviceName(x) ||
                x.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.')))
                return null;
            return path;
        }
    }

    private static bool IsDeviceName(string segment)
    {
        var stem = segment.Split('.')[0];
        return stem.Equals("CON", StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
            char.IsAsciiDigit(stem[3]);
    }
}

public interface IManagedAiSkillMarketplace
{
    Task<IReadOnlyList<SkillCatalogEntry>> ListAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AiSkill>> InstallAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AiSkill>> UpdateAsync(string id, CancellationToken cancellationToken);
}

public sealed record AiSkillSource(string CatalogId, string Repository, string SourcePath, string SourceRef)
{
    public bool HasSameOrigin(AiSkillSource other) =>
        string.Equals(CatalogId, other.CatalogId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Repository, other.Repository, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(SourcePath, other.SourcePath, StringComparison.Ordinal);

    public bool HasSameVersion(AiSkillSource other) => HasSameOrigin(other) &&
        string.Equals(SourceRef, other.SourceRef, StringComparison.OrdinalIgnoreCase);
}

// Downloads finish outside the runtime lease. The store owns validation, provenance and replacement.
public interface IManagedAiCatalogSkillStore
{
    Task<IReadOnlyList<AiSkill>> StoreCatalogAsync(string sourceDirectory, AiSkillSource source,
        AiSkillSource? expectedSource, CancellationToken cancellationToken);
}
