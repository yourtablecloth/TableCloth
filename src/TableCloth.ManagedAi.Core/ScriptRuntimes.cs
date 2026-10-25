namespace TableCloth.ManagedAi;

public interface IManagedAiScriptRuntimeManager
{
    Task<AiScriptRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken);
    Task<AiScriptRuntimeStatus> EnsureInstalledAsync(CancellationToken cancellationToken);
}

public sealed record AiScriptRuntimeComponentStatus(string Name, string ExpectedVersion,
    string? InstalledVersion, bool Installed, string? Failure);

// Null means the isolated probe could not establish support, not that support is available.
public sealed record AiScriptLongPathStatus(bool? PythonSupported, bool? NodeSupported, string? Failure);

public sealed record AiScriptRuntimeStatus(AiScriptRuntimeComponentStatus Python,
    AiScriptRuntimeComponentStatus Node, AiScriptLongPathStatus LongPaths);
