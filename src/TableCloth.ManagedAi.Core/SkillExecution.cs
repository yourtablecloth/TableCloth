namespace TableCloth.ManagedAi;

// Created by the trusted application, never supplied by a model or a remote catalog.
public sealed record AiSkillExecutionBridge(string Command, IReadOnlyList<string> Arguments,
    string WorkingDirectory, IReadOnlyList<string> SkillIds) : IDisposable
{
    public void Dispose()
    {
        // Dispose only the per-invocation manifest created by the application.
        // Never recurse or delete a caller-selected path.
        if (Arguments.Count != 2 || Arguments[0] != "--skill-mcp" ||
            !Path.IsPathFullyQualified(WorkingDirectory) || !Path.IsPathFullyQualified(Arguments[1])) return;
        var file = Path.GetFullPath(Arguments[1]);
        if (!string.Equals(Path.GetDirectoryName(file), Path.TrimEndingDirectorySeparator(Path.GetFullPath(WorkingDirectory)), StringComparison.OrdinalIgnoreCase)) return;
        var name = Path.GetFileName(file);
        if (!name.StartsWith("manifest-", StringComparison.Ordinal) || !name.EndsWith(".json", StringComparison.Ordinal) ||
            !Guid.TryParseExact(name[9..^5], "N", out _)) return;
        try { ManagedAiPaths.RejectReparsePoints(file); File.Delete(file); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ManagedAiException) { }
    }
}

public interface IManagedAiSkillExecution
{
    Task<AiSkillExecutionBridge?> PrepareAsync(IReadOnlyList<AiSkill> skills, string runDirectory,
        CancellationToken cancellationToken);
}
