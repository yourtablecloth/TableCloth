namespace TableCloth.ManagedAi;

// Applies to host-side API transport only. Workers retain their network denial.
public sealed record AiNetworkSettings(bool UseSystemProxyForSkills = false);

public interface IManagedAiNetworkSettings
{
    Task<AiNetworkSettings> GetAsync(CancellationToken cancellationToken);
}

public sealed class DefaultManagedAiNetworkSettings : IManagedAiNetworkSettings
{
    public Task<AiNetworkSettings> GetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AiNetworkSettings());
    }
}
