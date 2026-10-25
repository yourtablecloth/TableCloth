using System.Text.Json;

namespace TableCloth.ManagedAi.OpenAi;

public sealed class CodexJsonlParser
{
    private const int MaxActivities = 128;
    private const int MaxIdentityLength = 128;
    private readonly List<AiResponseActivity> _activities = [];
    private readonly Dictionary<string, int> _activityIndexes = new(StringComparer.Ordinal);
    public int EventCount { get; private set; }
    public int UnknownEvents { get; private set; }
    public int SearchCalls { get; private set; }
    public string Stage { get; private set; } = "Thinking";
    public IReadOnlyList<AiResponseActivity> Activities => Array.AsReadOnly(_activities.ToArray());
    public bool ActivityEvidenceIncomplete { get; private set; }
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
                    if (itemType.GetString() == "mcp_tool_call") ReadMcpActivity(item, completed: true);
                    break;
                case "item.started": case "item.updated":
                    if (root.TryGetProperty("item", out var active) && active.TryGetProperty("type", out var activeType))
                    {
                        Stage = activeType.GetString() switch { "web_search" => "Searching", "agent_message" => "Writing", _ => "Thinking" };
                        if (activeType.GetString() == "mcp_tool_call") ReadMcpActivity(active, completed: false);
                    }
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

    private void ReadMcpActivity(JsonElement item, bool completed)
    {
        // Provider event metadata is evidence of a tool exchange, not proof that an answer used a skill.
        // Retain no arguments, output, errors, paths, or model-supplied attribution.
        if (!TryReadIdentity(item, "id", out var id) || !TryReadIdentity(item, "server", out var server) ||
            !TryReadIdentity(item, "tool", out var tool))
        {
            ActivityEvidenceIncomplete = true;
            return;
        }

        var outcome = ReadMcpOutcome(item, completed);
        if (_activityIndexes.TryGetValue(id, out var index))
        {
            var previous = _activities[index];
            if (previous.Name != tool || previous.Server != server)
            {
                ActivityEvidenceIncomplete = true;
                return;
            }
            // A late start/update or an incomplete duplicate cannot erase a terminal result.
            if (completed && previous.Outcome != AiResponseActivityOutcome.Incomplete &&
                outcome != AiResponseActivityOutcome.Incomplete && previous.Outcome != outcome)
                ActivityEvidenceIncomplete = true;
            if (previous.Outcome == AiResponseActivityOutcome.Failed || outcome == AiResponseActivityOutcome.Incomplete) return;
            _activities[index] = previous with { Outcome = outcome };
            return;
        }
        if (_activities.Count >= MaxActivities)
        {
            ActivityEvidenceIncomplete = true;
            return;
        }
        _activityIndexes.Add(id, _activities.Count);
        _activities.Add(new(AiResponseActivityKind.McpTool, tool, outcome, server));
    }

    private AiResponseActivityOutcome ReadMcpOutcome(JsonElement item, bool completed)
    {
        var status = item.TryGetProperty("status", out var statusValue) && statusValue.ValueKind == JsonValueKind.String
            ? statusValue.GetString() : null;
        if (status == "failed" || item.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)
            return AiResponseActivityOutcome.Failed;

        var hasResult = item.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Object;
        if (hasResult)
        {
            // Codex 0.156.1 translates MCP isError into status=failed and omits it from the result.
            // Also accept the protocol flag defensively if a future event variant retains it.
            foreach (var field in new[] { "isError", "is_error" })
            {
                if (!result.TryGetProperty(field, out var isError)) continue;
                if (isError.ValueKind == JsonValueKind.True) return AiResponseActivityOutcome.Failed;
                if (isError.ValueKind != JsonValueKind.False)
                {
                    ActivityEvidenceIncomplete = true;
                    return AiResponseActivityOutcome.Incomplete;
                }
            }
        }
        if (!completed)
        {
            if (status != "in_progress") ActivityEvidenceIncomplete = true;
            return AiResponseActivityOutcome.Incomplete;
        }
        if (status == "completed" && hasResult && result.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.Array)
            return AiResponseActivityOutcome.Completed;
        ActivityEvidenceIncomplete = true;
        return AiResponseActivityOutcome.Incomplete;
    }

    private static bool TryReadIdentity(JsonElement item, string field, out string value)
    {
        value = string.Empty;
        if (!item.TryGetProperty(field, out var property) || property.ValueKind != JsonValueKind.String) return false;
        var text = property.GetString();
        if (string.IsNullOrEmpty(text) || text.Length > MaxIdentityLength ||
            text.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-' and not '.')) return false;
        value = text;
        return true;
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
