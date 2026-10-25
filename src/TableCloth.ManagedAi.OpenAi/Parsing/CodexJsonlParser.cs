using System.Text.Json;

namespace TableCloth.ManagedAi.OpenAi;

public sealed class CodexJsonlParser
{
    public int EventCount { get; private set; }
    public int UnknownEvents { get; private set; }
    public int SearchCalls { get; private set; }
    public string Stage { get; private set; } = "Thinking";
    private bool _completed;
    private bool _failed;
    private string? _lastMessage;
    private AiFailureCode _failure = AiFailureCode.ProviderFailed;

    public void AcceptDiagnostic(string line)
    {
        var code = CodexFailureClassifier.Classify(line);
        if (code != AiFailureCode.ProviderFailed) _failure = code;
    }

    public void Accept(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        try
        {
            using var json = JsonDocument.Parse(line, new JsonDocumentOptions { MaxDepth = 32 });
            EventCount++;
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var type) ||
                type.ValueKind != JsonValueKind.String) { UnknownEvents++; return; }
            switch (type.GetString())
            {
                case "turn.completed": _completed = true; break;
                case "turn.failed":
                    _failed = true;
                    ReadFailure(root);
                    break;
                case "error":
                    // Reconnect errors can be followed by a successful turn. Only turn.failed is terminal.
                    ReadFailure(root);
                    break;
                case "item.completed":
                    if (!root.TryGetProperty("item", out var item) || item.ValueKind != JsonValueKind.Object ||
                        !item.TryGetProperty("type", out var itemType)) break;
                    if (itemType.GetString() == "web_search") { SearchCalls++; Stage = "Thinking"; }
                    if (itemType.GetString() == "agent_message") { _lastMessage = item.GetProperty("text").GetString(); Stage = "Writing"; }
                    break;
                case "item.started": case "item.updated":
                    if (root.TryGetProperty("item", out var active) && active.TryGetProperty("type", out var activeType))
                        Stage = activeType.GetString() switch { "web_search" => "Searching", "agent_message" => "Writing", _ => "Thinking" };
                    break;
                case "thread.started": case "turn.started": break;
                default: UnknownEvents++; break;
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        { throw new ManagedAiException(AiFailureCode.InvalidStructuredOutput); }
    }

    public AiSearchResponse Complete(int exitCode, int maxResults)
    {
        var text = CompleteText(exitCode);
        if (SearchCalls == 0) throw new ManagedAiException(AiFailureCode.LiveSearchNotObserved);
        return SearchResultValidator.Validate(text, maxResults);
    }

    public string CompleteText(int exitCode)
    {
        if (exitCode != 0 || _failed) throw new ManagedAiException(_failure);
        if (!_completed || string.IsNullOrWhiteSpace(_lastMessage)) throw new ManagedAiException(AiFailureCode.InvalidStructuredOutput);
        if (_lastMessage.Length > 65536 || _lastMessage.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')))
            throw new ManagedAiException(AiFailureCode.OutputLimitExceeded);
        return _lastMessage;
    }

    private void ReadFailure(JsonElement root)
    {
        if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            AcceptDiagnostic(message.GetString()!);
        if (root.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String)
            AcceptDiagnostic(code.GetString()!);
        if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
        {
            if (error.TryGetProperty("message", out message) && message.ValueKind == JsonValueKind.String)
                AcceptDiagnostic(message.GetString()!);
            if (error.TryGetProperty("code", out code) && code.ValueKind == JsonValueKind.String)
                AcceptDiagnostic(code.GetString()!);
        }
    }
}
