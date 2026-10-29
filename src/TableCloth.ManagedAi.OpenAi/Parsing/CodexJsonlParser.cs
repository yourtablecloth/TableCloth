using System.Text;
using System.Text.Json;

namespace TableCloth.ManagedAi.OpenAi;

public sealed class CodexJsonlParser
{
    private const int MaximumTrackedCalls = 128;
    private readonly Dictionary<string, AiSkillToolRegistration> _skillTools = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PendingSkillCall> _pendingSkillCalls = new(StringComparer.Ordinal);
    private readonly HashSet<string> _finishedSkillCalls = new(StringComparer.Ordinal);
    private readonly List<AiSkillExecutionRecord> _skillExecutions = [];
    private bool _skillToolsConfigured;
    public IReadOnlyList<AiSkillExecutionRecord> SkillExecutions => _skillExecutions.AsReadOnly();
    public bool SkillExecutionsTruncated { get; private set; }
    public AiSkillExecutionRecord? LatestSkillExecution { get; private set; }
    public int EventCount { get; private set; }
    public int UnknownEvents { get; private set; }
    public int SearchCalls { get; private set; }
    public string Stage { get; private set; } = "Thinking";
    private bool _completed;
    private bool _failed;
    private string? _lastMessage;
    private AiFailureCode _failure = AiFailureCode.ProviderFailed;

    public CodexJsonlParser(IReadOnlyList<AiSkillToolRegistration>? tools = null)
    {
        if (tools is not null) ConfigureSkillTools(tools);
    }

    public void ConfigureSkillTools(IReadOnlyList<AiSkillToolRegistration> tools)
    {
        if (_skillToolsConfigured || EventCount != 0) throw new InvalidOperationException();
        _skillToolsConfigured = true;
        // The application registration supplies identity. A tool result cannot choose its skill label.
        var valid = tools.Take(128).Where(ValidRegistration).ToArray();
        var unique = valid.GroupBy(tool => tool.ToolName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1).Select(group => group.Single()).ToArray();
        foreach (var group in unique.GroupBy(tool => tool.ToolName.Replace('-', '_'), StringComparer.Ordinal))
        {
            var unambiguous = group.Count() == 1;
            foreach (var tool in group)
            {
                // Some CLI versions normalize '-' to '_'. Do not guess when two registrations collide.
                if (unambiguous || tool.ToolName != group.Key) _skillTools[tool.ToolName] = tool;
                if (unambiguous) _skillTools[group.Key] = tool;
            }
        }
    }

    private static bool ValidRegistration(AiSkillToolRegistration tool)
    {
        if (tool is null) return false;
        try
        {
            AiSkillExecutionHistory.Validate([new("registration", tool.ToolName, tool.SkillId, tool.SkillName,
                tool.EntryPoint, AiSkillExecutionStatus.Unverified, null, false, false, false, null)]);
            return true;
        }
        catch (ManagedAiException) { return false; }
    }

    public void AcceptDiagnostic(string line)
    {
        var code = CodexFailureClassifier.Classify(line);
        if (code != AiFailureCode.ProviderFailed) _failure = code;
    }

    public void Accept(string line)
    {
        LatestSkillExecution = null;
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
                    if (itemType.GetString() == "mcp_tool_call") ReadSkillCall(item, completed: true);
                    break;
                case "item.started": case "item.updated":
                    if (root.TryGetProperty("item", out var active) && active.TryGetProperty("type", out var activeType))
                    {
                        Stage = activeType.GetString() switch { "web_search" => "Searching", "agent_message" => "Writing", _ => "Thinking" };
                        if (type.GetString() == "item.started" && activeType.GetString() == "mcp_tool_call")
                            ReadSkillCall(active, completed: false);
                    }
                    break;
                case "thread.started": case "turn.started": break;
                default: UnknownEvents++; break;
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        { throw new ManagedAiException(AiFailureCode.InvalidStructuredOutput); }
    }

    private void ReadSkillCall(JsonElement item, bool completed)
    {
        if (Text(item, "server") != "tablecloth_skills" || Text(item, "tool") is not { } toolName ||
            !_skillTools.TryGetValue(toolName, out var tool) || Text(item, "id") is not { } id ||
            !Token(id, 128)) return;
        if (_finishedSkillCalls.Contains(id)) return;
        var validArguments = item.TryGetProperty("arguments", out var arguments) && arguments.ValueKind == JsonValueKind.Object &&
            Encoding.UTF8.GetByteCount(arguments.GetRawText()) <= 131072;
        if (!completed)
        {
            if (_pendingSkillCalls.TryGetValue(id, out var duplicate))
            { _pendingSkillCalls[id] = duplicate with { Valid = false }; return; }
            if (_finishedSkillCalls.Count + _pendingSkillCalls.Count >= MaximumTrackedCalls)
            { SkillExecutionsTruncated = true; return; }
            var valid = validArguments && Text(item, "status") == "in_progress" && Null(item, "error") &&
                Null(item, "result") && UniqueProperties(item);
            _pendingSkillCalls.Add(id, new(toolName, validArguments ? arguments.Clone() : default, valid));
            return;
        }
        var paired = _pendingSkillCalls.Remove(id, out var started) && started.Valid && validArguments &&
            started.ToolName == toolName && JsonElement.DeepEquals(started.Arguments, arguments) && UniqueProperties(item);
        if (_finishedSkillCalls.Count >= MaximumTrackedCalls)
        { SkillExecutionsTruncated = true; return; }
        _finishedSkillCalls.Add(id);
        var record = new AiSkillExecutionRecord(id, tool.ToolName, tool.SkillId, tool.SkillName, tool.EntryPoint,
            AiSkillExecutionStatus.Unverified, null, false, false, false, paired ? "unverified-result" : "unpaired-tool-call");
        if (paired && Text(item, "status") == "completed" && Null(item, "error") &&
            item.TryGetProperty("result", out var result)) record = ReadExecution(result, record);
        if (_skillExecutions.Count == AiSkillExecutionHistory.MaximumRecords)
        { SkillExecutionsTruncated = true; return; }
        _skillExecutions.Add(record);
        LatestSkillExecution = record;
    }

    private static AiSkillExecutionRecord ReadExecution(JsonElement result, AiSkillExecutionRecord unverified)
    {
        if (result.ValueKind != JsonValueKind.Object || !UniqueProperties(result) ||
            result.TryGetProperty("isError", out var isError) && isError.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            !result.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array || content.GetArrayLength() != 1 ||
            !UniqueProperties(content[0]) || Text(content[0], "type") != "text" || Text(content[0], "text") is not { } text ||
            text.Length > 1048576) return unverified;
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 8 });
            var value = document.RootElement;
            if (value.ValueKind != JsonValueKind.Object || !UniqueProperties(value) ||
                Text(value, "status") is not ("completed" or "failed" or "blocked") ||
                !value.TryGetProperty("exitCode", out var exit) || exit.ValueKind != JsonValueKind.Null && !exit.TryGetInt32(out _) ||
                !Boolean(value, "wasAppContainer", out var contained) || !Boolean(value, "timedOut", out var timedOut) ||
                !Boolean(value, "outputLimited", out var limited) || !value.TryGetProperty("failure", out var failure) ||
                failure.ValueKind != JsonValueKind.Null && (failure.ValueKind != JsonValueKind.String || !Token(failure.GetString()!, 64)) ||
                Text(value, "stdout") is null || Text(value, "stderr") is null) return unverified;
            var code = exit.ValueKind == JsonValueKind.Null ? (int?)null : exit.GetInt32();
            var status = Text(value, "status") switch
            {
                "completed" => AiSkillExecutionStatus.Completed,
                "failed" => AiSkillExecutionStatus.Failed,
                _ => AiSkillExecutionStatus.Blocked
            };
            var record = unverified with { Status = status, ExitCode = code, WasAppContainer = contained,
                TimedOut = timedOut, OutputLimited = limited, Failure = failure.ValueKind == JsonValueKind.Null ? null : failure.GetString() };
            // CLI JSONL omits isError=false. When present it must agree with the broker status.
            if (isError.ValueKind != JsonValueKind.Undefined && isError.GetBoolean() != (status != AiSkillExecutionStatus.Completed) ||
                status == AiSkillExecutionStatus.Completed && !record.Succeeded ||
                status == AiSkillExecutionStatus.Blocked && contained ||
                status == AiSkillExecutionStatus.Failed && code is null && !timedOut && !limited && record.Failure is null)
                return unverified;
            return record;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException) { return unverified; }
    }

    private sealed record PendingSkillCall(string ToolName, JsonElement Arguments, bool Valid);
    private static string? Text(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static bool Null(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Null;
    private static bool Boolean(JsonElement value, string name, out bool result)
    {
        result = false;
        if (!value.TryGetProperty(name, out var property) || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
        result = property.GetBoolean(); return true;
    }
    private static bool Token(string value, int maximum) => value.Length is > 0 && value.Length <= maximum &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
    private static bool UniqueProperties(JsonElement value) => value.ValueKind == JsonValueKind.Object &&
        value.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() == value.EnumerateObject().Count();

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
