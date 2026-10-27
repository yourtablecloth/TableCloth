using System.Threading;
using System.Threading.Tasks;
using TableCloth.ManagedAi;

namespace TableCloth.Components.Implementations;

/// <summary>Reads the existing compatibility preference for each AI request.</summary>
public sealed class ManagedAiNetworkSettings(IPreferencesManager preferences) : IManagedAiNetworkSettings
{
    public async Task<AiNetworkSettings> GetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = await preferences.LoadPreferencesAsync(cancellationToken).ConfigureAwait(false);
        // PreferencesManager can fall back after a failed read, including cancellation.
        cancellationToken.ThrowIfCancellationRequested();
        return new(UseSystemProxyForSkills: settings?.EnableZScalerRootCertPropagation == true);
    }
}
