using System;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models;

namespace TableCloth.Components.Implementations;

public sealed class StandaloneAppUpdateManager(
    IResourceResolver resourceResolver) : IAppUpdateManager
{
    private readonly string owner = "yourtablecloth";
    private readonly string repo = "TableCloth";

    public async Task<ApiInvokeResult<Uri?>> QueryNewVersionDownloadUrlAsync(
        CancellationToken cancellationToken = default)
    {
        var thisVersion = typeof(IAppUpdateManager).Assembly.GetName().Version;
        var latestVersion = await resourceResolver.GetLatestVersionAsync(owner, repo, cancellationToken).ConfigureAwait(false);

        if (Version.TryParse(latestVersion, out var parsedVersion) &&
            thisVersion != null && parsedVersion > thisVersion)
        {
            var targetUrl = await resourceResolver.GetReleaseDownloadUrlAsync(owner, repo, cancellationToken).ConfigureAwait(false);
            return targetUrl;
        }
        else
            return default;
    }
}
