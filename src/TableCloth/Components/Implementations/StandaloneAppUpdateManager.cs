using System;
using System.Threading.Tasks;

namespace TableCloth.Components.Implementations;

public sealed class StandaloneAppUpdateManager(
    IResourceResolver resourceResolver) : IAppUpdateManager
{
    private readonly string owner = "yourtablecloth";
    private readonly string repo = "TableCloth";

    public async Task<string?> QueryNewVersionDownloadUrl()
    {
        var thisVersion = typeof(IAppUpdateManager).Assembly.GetName().Version;
        var latestVersion = await resourceResolver.GetLatestVersion(owner, repo).ConfigureAwait(false);

        if (Version.TryParse(latestVersion, out var parsedVersion) &&
            thisVersion != null && parsedVersion > thisVersion)
        {
            var targetUrl = await resourceResolver.GetDownloadUrl(owner, repo).ConfigureAwait(false);
            return targetUrl.AbsoluteUri;
        }
        else
            return default;
    }
}
