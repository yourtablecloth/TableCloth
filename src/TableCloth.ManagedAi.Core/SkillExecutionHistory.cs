namespace TableCloth.ManagedAi;

// Tool-to-skill identity comes from the application registration, never a model's answer.
public sealed record AiSkillToolRegistration(string ToolName, string SkillId, string SkillName, string EntryPoint);

public enum AiSkillExecutionStatus { Completed, Failed, Blocked, Unverified }

// Only execution facts survive the ephemeral provider turn. Script input/output and host paths do not.
public sealed record AiSkillExecutionRecord(string CallId, string ToolName, string SkillId, string SkillName,
    string EntryPoint, AiSkillExecutionStatus Status, int? ExitCode, bool WasAppContainer,
    bool TimedOut, bool OutputLimited, string? Failure)
{
    public bool Succeeded => Status == AiSkillExecutionStatus.Completed && ExitCode == 0 && WasAppContainer &&
        !TimedOut && !OutputLimited && Failure is null;
}

public static class AiSkillExecutionHistory
{
    public const int MaximumRecords = 16;

    public static IReadOnlyList<AiSkillExecutionRecord> Snapshot(IReadOnlyList<AiSkillExecutionRecord> records)
    {
        if (records is null || records.Count > MaximumRecords) throw Invalid();
        var snapshot = records.ToArray();
        Validate(snapshot);
        return Array.AsReadOnly(snapshot);
    }

    public static void Validate(IReadOnlyList<AiSkillExecutionRecord> records)
    {
        if (records is null || records.Count > MaximumRecords) throw Invalid();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            if (record is null || !Token(record.CallId, 128) || !ids.Add(record.CallId) ||
                !Token(record.ToolName, 64) || !Token(record.SkillId, 64) || !Label(record.SkillName, 128) ||
                record.EntryPoint is null || record.EntryPoint.Length > 240 ||
                record.EntryPoint.Any(char.IsControl) || record.EntryPoint.Contains(':') ||
                record.EntryPoint.Contains('\\') || record.EntryPoint.StartsWith('/') ||
                record.EntryPoint.Split('/').Any(part => part is "." or "..") ||
                !Enum.IsDefined(record.Status) || record.Failure is not null && !Token(record.Failure, 64) ||
                record.Status == AiSkillExecutionStatus.Completed && !record.Succeeded)
                throw Invalid();
        }
    }

    // Include metadata in the same conversation budget as visible messages.
    public static int Measure(IReadOnlyList<AiSkillExecutionRecord> records)
        => records.Sum(record => record.CallId.Length + record.ToolName.Length + record.SkillId.Length +
            record.SkillName.Length + record.EntryPoint.Length + (record.Failure?.Length ?? 0) + 160);

    private static bool Token(string? value, int maximum) => value is { Length: > 0 } && value.Length <= maximum &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');
    private static bool Label(string? value, int maximum) => value is { Length: > 0 } && value.Length <= maximum &&
        !value.Any(char.IsControl);
    private static ManagedAiException Invalid() => new(AiFailureCode.InvalidStructuredOutput);
}
