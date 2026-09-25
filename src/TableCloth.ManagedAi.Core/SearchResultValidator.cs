using System.Text.Json;

namespace TableCloth.ManagedAi;

public static class SearchResultValidator
{
    public static AiSearchResponse Validate(string json, int maxResults = 5)
    {
        try
        {
            if (json.Length > 64 * 1024 || maxResults is < 1 or > 5) throw Invalid();
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 10 });
            var root = document.RootElement;
            RequireProperties(root, "answer", "results", "warnings");
            var answer = Text(root.GetProperty("answer"), 1000);
            var results = root.GetProperty("results");
            var warnings = root.GetProperty("warnings");
            if (results.ValueKind != JsonValueKind.Array || results.GetArrayLength() > maxResults ||
                warnings.ValueKind != JsonValueKind.Array || warnings.GetArrayLength() > 5) throw Invalid();
            var messages = warnings.EnumerateArray().Select(x => Text(x, 300)).ToList();
            var candidates = new List<AiSearchCandidate>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool removed = false;
            foreach (var item in results.EnumerateArray())
            {
                RequireProperties(item, "title", "target_url", "description", "source_name", "source_url", "reason");
                var title = Text(item.GetProperty("title"), 120);
                var description = Text(item.GetProperty("description"), 500);
                var source = Text(item.GetProperty("source_name"), 120);
                var reason = Text(item.GetProperty("reason"), 500);
                var targetText = Text(item.GetProperty("target_url"), 2048);
                var sourceText = Text(item.GetProperty("source_url"), 2048);
                if (!PublicHttpsUrl.TryParse(targetText, out var target) || !PublicHttpsUrl.TryParse(sourceText, out var sourceUri))
                {
                    removed = true;
                    continue;
                }
                if (seen.Add(target.AbsoluteUri))
                    candidates.Add(new(title, target, description, source, sourceUri, reason));
            }
            if (removed) messages.Add("안전한 HTTPS 주소 조건을 충족하지 못한 후보를 제외했습니다.");
            return new(answer, candidates.AsReadOnly(), messages.AsReadOnly(), DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw Invalid();
        }
    }

    public static void ValidateRequest(AiSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query) || request.Query.Length > 4000 ||
            request.Query.Any(char.IsControl) || request.MaxResults is < 1 or > 5)
            throw new ManagedAiException(AiFailureCode.InvalidQuery);
    }

    private static void RequireProperties(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) throw Invalid();
        var found = element.EnumerateObject().Select(x => x.Name).ToArray();
        if (found.Length != names.Length || found.Distinct(StringComparer.Ordinal).Count() != names.Length ||
            names.Except(found, StringComparer.Ordinal).Any()) throw Invalid();
    }

    private static string Text(JsonElement element, int maximum)
    {
        if (element.ValueKind != JsonValueKind.String) throw Invalid();
        var text = element.GetString()!;
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximum ||
            text.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t')) throw Invalid();
        return text;
    }
    private static ManagedAiException Invalid() => new(AiFailureCode.InvalidStructuredOutput);
}
