using System;
using System.Collections.Generic;
using System.Linq;
using TableCloth.ManagedAi;
using TableCloth.Models.Catalog;

namespace TableCloth;

public enum QuickStartRouteKind
{
    EmptySandbox,
    WebAddress,
    CatalogSearch,
    SpecialCommand,
    AiQuestion,
}

public readonly record struct QuickStartRoute(QuickStartRouteKind Kind, string Value);

public static class QuickStartInputRouter
{
    public static QuickStartRoute Classify(string? input, IEnumerable<CatalogInternetService> services)
    {
        var text = input?.Trim() ?? string.Empty;
        if (text.Length == 0)
            return new(QuickStartRouteKind.EmptySandbox, string.Empty);

        if (text.StartsWith('#'))
            return new(QuickStartRouteKind.SpecialCommand, text);

        if (PublicWebUrl.TryParse(text, out _))
            return new(QuickStartRouteKind.WebAddress, text);

        // Keep malformed or blocked URL attempts in the address path so the existing
        // launcher can explain why they cannot be opened.
        if (text.Contains("://", StringComparison.Ordinal))
            return new(QuickStartRouteKind.WebAddress, text);

        if (!text.Any(char.IsWhiteSpace) && text.Contains('.')
            && Uri.TryCreate("https://" + text, UriKind.Absolute, out var bareAddress)
            && bareAddress.Host.Contains('.'))
            return new(QuickStartRouteKind.WebAddress, "https://" + text);

        if (text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length <= 2
            && services.Any(service => MatchesCatalogName(service, text)))
            return new(QuickStartRouteKind.CatalogSearch, text);

        return new(QuickStartRouteKind.AiQuestion, text);
    }

    public static bool MatchesCatalogName(CatalogInternetService service, string query)
        => query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .All(word => service.DisplayName?.Contains(word, StringComparison.CurrentCultureIgnoreCase) == true);
}
