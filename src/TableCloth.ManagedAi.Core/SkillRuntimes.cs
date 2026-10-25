namespace TableCloth.ManagedAi;

public sealed record SkillRuntimeInstallation(string Version, string RootDirectory, string EntryPoint);
public sealed record SkillRuntimeEnvironment(SkillRuntimeInstallation NodeJs, SkillRuntimeInstallation Python);

public interface IManagedAiSkillRuntimeManager
{
    Task<SkillRuntimeEnvironment?> GetActiveAsync(CancellationToken cancellationToken);
    Task<SkillRuntimeEnvironment> InstallAsync(IProgress<AiProgress>? progress, CancellationToken cancellationToken);
}
