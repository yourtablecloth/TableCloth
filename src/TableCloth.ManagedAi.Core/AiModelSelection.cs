using System.Globalization;

namespace TableCloth.ManagedAi;

// Standard-speed Codex credits per one million text tokens, not API dollars or a bill estimate.
public sealed record AiModelTokenCost(decimal Input, decimal CachedInput, decimal Output, DateOnly VerifiedOn)
{
    // A bundled rate card is advisory. Stop using it after 30 days until it is verified again.
    public bool IsCurrent(DateOnly today) => today >= VerifiedOn && today.DayNumber - VerifiedOn.DayNumber < 30 &&
        Input is >= 0 and <= 1_000_000_000 && CachedInput is >= 0 and <= 1_000_000_000 && Output is >= 0 and <= 1_000_000_000;
}

public static class AiModelSelection
{
    public static AiModel Select(IReadOnlyList<AiModel> models, string? preferredId, DateOnly today)
    {
        if (models.Count == 0) throw new ManagedAiException(AiFailureCode.ModelListUnavailable);
        if (models.FirstOrDefault(x => x.Id == preferredId) is { } preferred) return preferred;

        // Compare only verified rates on the same basis: 1M uncached input + 1M output tokens.
        // Unknown costs are not zero. Ties retain provider order.
        var cheapest = models.Where(x => x.TokenCost?.IsCurrent(today) == true)
            .OrderBy(x => x.TokenCost!.Input + x.TokenCost.Output).FirstOrDefault();
        if (cheapest is not null) return cheapest;

        return models.Select(model => (Model: model, Version: LunaVersion(model.Id)))
            .Where(x => x.Version is not null).OrderByDescending(x => x.Version)
            .Select(x => x.Model).FirstOrDefault() ?? models[0];
    }

    private static (Version Generation, DateOnly Snapshot)? LunaVersion(string id)
    {
        if (!id.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase)) return null;
        var end = id.IndexOf("-luna", StringComparison.OrdinalIgnoreCase);
        if (end <= 4) return null;
        var versionText = id[4..end];
        if (!Version.TryParse(versionText.Contains('.') ? versionText : versionText + ".0", out var version)) return null;
        var suffix = id[(end + 5)..];
        // The moving alias represents the latest snapshot within its generation.
        var date = DateOnly.MaxValue;
        if (suffix.Length != 0 && (suffix[0] != '-' || !DateOnly.TryParseExact(suffix[1..], "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out date))) return null;
        return (new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision)), date);
    }
}
