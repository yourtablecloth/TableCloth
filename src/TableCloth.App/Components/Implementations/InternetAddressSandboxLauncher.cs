using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.ManagedAi;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;

namespace TableCloth.Components.Implementations;

public sealed class CatalogServiceLauncher(IAppUserInterface userInterface) : ICatalogServiceLauncher
{
    public Task<bool> LaunchAsync(System.Collections.Generic.IReadOnlyList<CatalogInternetService> services,
        string targetUrl, CancellationToken cancellationToken)
        => userInterface.CreateQuickStartPageViewModel()
            .LaunchForDeepLinkAsync(services, targetUrl, cancellationToken);
}

public sealed class InternetAddressSandboxLauncher(
    IResourceCacheManager resources,
    ICatalogServiceChoice catalogChoice,
    ICatalogServiceLauncher catalogLauncher,
    ISandboxLauncher sandboxLauncher) : IInternetAddressSandboxLauncher
{
    public async Task<InternetAddressLaunchResult> LaunchAsync(
        string address, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!PublicWebUrl.TryParse(address?.Trim(), out var target))
            return InternetAddressLaunchResult.InvalidAddress;

        CatalogDocument catalog;
        try
        {
            try { catalog = resources.CatalogDocument; }
            catch (ArgumentNullException) { catalog = await resources.LoadCatalogDocumentAsync(cancellationToken); }
        }
        catch (OperationCanceledException) { throw; }
        catch { return InternetAddressLaunchResult.CatalogUnavailable; }

        if (catalog is null)
            return InternetAddressLaunchResult.CatalogUnavailable;

        var match = CatalogTargetUrlMatcher.Match(catalog, target.OriginalString);
        if (match.Reason == CatalogTargetUrlRejectionReason.AmbiguousCandidates)
        {
            var candidates = catalog.Services
                .Where(service => match.ServiceIds.Contains(service.Id, StringComparer.Ordinal))
                .ToArray();
            var selectedId = await catalogChoice.SelectAsync(target, candidates, cancellationToken);
            if (selectedId is null)
                return InternetAddressLaunchResult.Canceled;
            if (!candidates.Any(service => string.Equals(service.Id, selectedId, StringComparison.Ordinal)))
                return InternetAddressLaunchResult.LaunchFailed;
            match = CatalogTargetUrlMatcher.Match(catalog, target.OriginalString, [selectedId]);
            if (!match.IsAccepted)
                return InternetAddressLaunchResult.LaunchFailed;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (match.IsAccepted)
        {
            var services = catalog.Services
                .Where(service => match.ServiceIds.Contains(service.Id, StringComparer.Ordinal))
                .ToArray();
            if (services.Length == 0)
                return InternetAddressLaunchResult.CatalogUnavailable;

            return await catalogLauncher.LaunchAsync(services, match.AcceptedUrl, cancellationToken)
                ? InternetAddressLaunchResult.CatalogServiceLaunched
                : InternetAddressLaunchResult.LaunchFailed;
        }

        if (match.Reason != CatalogTargetUrlRejectionReason.NoCatalogDomainMatch)
            return InternetAddressLaunchResult.InvalidAddress;

        return await sandboxLauncher.RunSandboxAsync(new TableClothConfiguration
        {
            BrowserOnlyUrl = target.AbsoluteUri,
        }, cancellationToken)
            ? InternetAddressLaunchResult.BrowserOnlyLaunched
            : InternetAddressLaunchResult.LaunchFailed;
    }
}
