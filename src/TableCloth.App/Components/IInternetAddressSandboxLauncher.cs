using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.Catalog;

namespace TableCloth.Components;

public enum InternetAddressLaunchResult
{
    InvalidAddress,
    CatalogUnavailable,
    Canceled,
    LaunchFailed,
    CatalogServiceLaunched,
    BrowserOnlyLaunched,
}

public interface IInternetAddressSandboxLauncher
{
    Task<InternetAddressLaunchResult> LaunchAsync(string address, CancellationToken cancellationToken = default);
}

public interface ICatalogServiceChoice
{
    Task<string?> SelectAsync(System.Uri target, IReadOnlyList<CatalogInternetService> candidates,
        CancellationToken cancellationToken);
}

public interface ICatalogServiceLauncher
{
    Task<bool> LaunchAsync(IReadOnlyList<CatalogInternetService> services, string targetUrl,
        CancellationToken cancellationToken);
}
