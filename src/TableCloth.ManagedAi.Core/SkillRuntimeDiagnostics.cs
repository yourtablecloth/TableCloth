namespace TableCloth.ManagedAi;

public enum WindowsLongPathPolicy { Enabled, Disabled, Unsupported, Unknown }
public enum LongPathSupport { Supported, Limited, NotInstalled, Unknown }

public sealed record SkillRuntimeDiagnosticsResult(WindowsLongPathPolicy WindowsPolicy,
    LongPathSupport ManagedIo, LongPathSupport NodeJs, LongPathSupport Python);

public interface IManagedAiSkillRuntimeDiagnostics
{
    Task<SkillRuntimeDiagnosticsResult> CheckAsync(CancellationToken cancellationToken);
}
