using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Components;
using TableCloth.Models.Catalog;

namespace TableCloth.ManagedAi;

public interface IManagedAiCatalogLauncher
{
    Task<bool> LaunchAsync(IReadOnlyList<CatalogInternetService> services, string targetUrl, CancellationToken cancellationToken);
}

// Reuse the existing Quick Start composition: preferences, data/NPKI mappings, guest Spork, then the original URL.
public sealed class ManagedAiCatalogLauncher(IAppUserInterface userInterface) : IManagedAiCatalogLauncher
{
    public Task<bool> LaunchAsync(IReadOnlyList<CatalogInternetService> services, string targetUrl, CancellationToken cancellationToken)
        => userInterface.CreateQuickStartPageViewModel().LaunchForDeepLinkAsync(services, targetUrl, cancellationToken);
}
