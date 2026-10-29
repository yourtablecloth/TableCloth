using System;
using System.Collections.Generic;
using System.Linq;
using TableCloth.ManagedAi;
using TableCloth.Models.Catalog;

namespace TableCloth;

public enum QuickStartSuggestionKind
{
    CatalogName,
    WebAddress,
}

public sealed record QuickStartSuggestion(
    QuickStartSuggestionKind Kind,
    string DisplayName,
    string Url)
{
    public string Completion => Url;
}

public static class QuickStartSuggestionSearch
{
    public static IReadOnlyList<QuickStartSuggestion> Find(
        string? input, IEnumerable<CatalogInternetService> services, int limit = 5)
    {
        var query = input?.Trim() ?? string.Empty;
        if (query.Length == 0 || query.StartsWith('#') || limit <= 0)
            return Array.Empty<QuickStartSuggestion>();

        var addressQuery = GetAddressQuery(query);
        if (addressQuery is not null)
        {
            if (addressQuery.Length == 0)
                return Array.Empty<QuickStartSuggestion>();

            return services
                .Where(service => PublicWebUrl.TryParse(service.Url, out var url)
                    && (url.Host.StartsWith(addressQuery, StringComparison.OrdinalIgnoreCase)
                        || (url.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                            && url.Host[4..].StartsWith(addressQuery, StringComparison.OrdinalIgnoreCase))
                        || service.Url.StartsWith(query, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(service => service.DisplayName, StringComparer.CurrentCulture)
                .Take(limit)
                .Select(service => new QuickStartSuggestion(
                    QuickStartSuggestionKind.WebAddress, service.DisplayName, service.Url))
                .ToArray();
        }

        if (query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length > 2)
            return Array.Empty<QuickStartSuggestion>();

        return services
            .Where(service => QuickStartInputRouter.MatchesCatalogName(service, query))
            .OrderByDescending(service => service.DisplayName.StartsWith(query, StringComparison.CurrentCultureIgnoreCase))
            .ThenBy(service => service.DisplayName, StringComparer.CurrentCulture)
            .Take(limit)
            .Select(service => new QuickStartSuggestion(
                QuickStartSuggestionKind.CatalogName, service.DisplayName, service.Url))
            .ToArray();
    }

    private static string? GetAddressQuery(string query)
    {
        if (query.Any(char.IsWhiteSpace))
            return null;

        var withoutScheme = query;
        if (query.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            withoutScheme = query[8..];
        else if (query.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            withoutScheme = query[7..];
        else if (!query.Contains('.') && !query.StartsWith("www", StringComparison.OrdinalIgnoreCase))
            return null;

        return withoutScheme.Split(['/', '?', '#'], 2)[0];
    }
}
