using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Components;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;

namespace TableCloth.ManagedAi;

public sealed class TableClothBrowserAdapter(ISandboxLauncher launcher, IResourceCacheManager resources,
    IManagedAiCatalogLauncher catalogLauncher, IManagedAiCatalogChoice choice,
    IManagedAiLinkOpenChoice openChoice, IManagedAiHostBrowser hostBrowser) : ITableClothBrowser
{
    public async Task OpenAsync(Uri target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var safe = PublicWebUrl.Validate(target.OriginalString);
        switch (await openChoice.SelectAsync(safe, cancellationToken))
        {
            case ManagedAiLinkOpenDestination.Cancel:
                return;
            case ManagedAiLinkOpenDestination.CurrentBrowser:
                await hostBrowser.OpenAsync(safe, cancellationToken);
                return;
            case ManagedAiLinkOpenDestination.WindowsSandbox:
                break;
            default:
                throw new ManagedAiException(AiFailureCode.BrowserOpenFailed);
        }
        CatalogDocument? catalog;
        try
        {
            // The normal app startup already loads the catalog; only recover an absent cache here.
            try { catalog = resources.CatalogDocument; }
            catch (ArgumentNullException) { catalog = null; }
            catalog ??= await resources.LoadCatalogDocumentAsync(cancellationToken);
            if (catalog is null) throw new ManagedAiException(AiFailureCode.CatalogUnavailable);
        }
        catch (OperationCanceledException) { throw; }
        catch { throw new ManagedAiException(AiFailureCode.CatalogUnavailable); }

        var match = CatalogTargetUrlMatcher.Match(catalog, safe.OriginalString);
        if (match.Reason == CatalogTargetUrlRejectionReason.AmbiguousCandidates)
        {
            var candidates = catalog.Services.Where(x => match.ServiceIds.Contains(x.Id, StringComparer.Ordinal)).ToArray();
            var selectedId = await choice.SelectAsync(safe, candidates, cancellationToken);
            if (selectedId is null) throw new OperationCanceledException(cancellationToken);
            // Never trust a dialog/extension to return a service outside the actual candidate set.
            if (!candidates.Any(x => x.Id == selectedId)) throw new ManagedAiException(AiFailureCode.BrowserOpenFailed);
            match = CatalogTargetUrlMatcher.Match(catalog, safe.OriginalString, [selectedId]);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (match.IsAccepted)
        {
            var services = catalog.Services.Where(x => match.ServiceIds.Contains(x.Id, StringComparer.Ordinal)).ToArray();
            if (services.Length == 0 || !await catalogLauncher.LaunchAsync(services, match.AcceptedUrl, cancellationToken))
                throw new ManagedAiException(AiFailureCode.BrowserOpenFailed);
            return;
        }
        if (match.Reason != CatalogTargetUrlRejectionReason.NoCatalogDomainMatch)
            throw new ManagedAiException(AiFailureCode.UnsafeUrl);

        if (!await launcher.RunSandboxAsync(new TableClothConfiguration
        {
            ManagedAiBrowserOnlyUrl = safe.AbsoluteUri
        }, cancellationToken)) throw new ManagedAiException(AiFailureCode.BrowserOpenFailed);
    }

}
