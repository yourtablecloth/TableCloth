using System.Text.Json;

namespace TableCloth.ManagedAi.OpenAi;

// Only initializes the transport and lists models. Never starts a thread or a turn.
public sealed class CodexModelListExchange
{
    public const string Initialize = "{\"id\":1,\"method\":\"initialize\",\"params\":{\"clientInfo\":{\"name\":\"tablecloth\",\"title\":\"TableCloth\",\"version\":\"0.1.0\"}}}\n";
    private readonly List<AiModel> _models = [];
    private readonly HashSet<string> _cursors = [];
    private int _expectedId = 1;
    private bool _complete;

    public ProcessInputReply? Accept(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        try
        {
            using var json = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 32 });
            var root = json.RootElement;
            if (!root.TryGetProperty("id", out var id) || !id.TryGetInt32(out var number) || number != _expectedId) return null;
            if (_complete || root.TryGetProperty("error", out _)) throw Unavailable();
            var result = root.GetProperty("result");
            if (_expectedId == 1)
            {
                _expectedId++;
                return new("{\"method\":\"initialized\",\"params\":{}}\n" + Request(null));
            }
            foreach (var entry in result.GetProperty("data").EnumerateArray())
            {
                if (entry.TryGetProperty("hidden", out var hidden) && hidden.GetBoolean()) continue;
                if (entry.TryGetProperty("inputModalities", out var modalities) &&
                    !modalities.EnumerateArray().Any(x => x.GetString() == "text")) continue;
                var model = entry.GetProperty("model").GetString()!;
                AiModel.ValidateId(model);
                var name = entry.GetProperty("displayName").GetString();
                if (string.IsNullOrWhiteSpace(name) || name.Length > 128 || name.Any(char.IsControl)) throw Unavailable();
                if (_models.All(x => x.Id != model))
                    _models.Add(new(model, name, entry.TryGetProperty("isDefault", out var isDefault) && isDefault.GetBoolean()));
                if (_models.Count > 256) throw Unavailable();
            }
            if (result.TryGetProperty("nextCursor", out var next) && next.ValueKind != JsonValueKind.Null)
            {
                var cursor = next.GetString()!;
                if (cursor.Length is 0 or > 1024 || !_cursors.Add(cursor) || _expectedId >= 10) throw Unavailable();
                _expectedId++;
                return new(Request(cursor));
            }
            _complete = true;
            return new(Close: true);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or ManagedAiException)
        { throw Unavailable(); }
    }

    public IReadOnlyList<AiModel> Complete(int exitCode)
        => exitCode == 0 && _complete && _models.Count > 0 ? _models.AsReadOnly() : throw Unavailable();

    private string Request(string? cursor)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", _expectedId);
            writer.WriteString("method", "model/list");
            writer.WriteStartObject("params");
            writer.WriteNumber("limit", 100);
            writer.WriteBoolean("includeHidden", false);
            writer.WriteString("cursor", cursor);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray()) + "\n";
    }
    private static ManagedAiException Unavailable() => new(AiFailureCode.ModelListUnavailable);
}
