using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models;
using Velopack;
using Velopack.Sources;

namespace TableCloth.Components.Implementations;

public sealed class AppUpdateManager : IAppUpdateManager
{
    private readonly IResourceResolver _resourceResolver;
    private readonly ILogger<AppUpdateManager> _logger;
    private readonly UpdateManager _updateManager;
    private UpdateInfo? _updateInfo;

    private const string owner = "yourtablecloth";
    private const string repo = "TableCloth";

    public AppUpdateManager(
        IResourceResolver resourceResolver,
        ILogger<AppUpdateManager> logger)
    {
        _resourceResolver = resourceResolver;
        _logger = logger;
        _updateManager = new UpdateManager(
            new GithubSource($"https://github.com/{owner}/{repo}", null, false));
    }

    public async Task<ApiInvokeResult<Uri?>> QueryNewVersionDownloadUrlAsync(
        CancellationToken cancellationToken = default)
    {
        var thisVersion = typeof(IAppUpdateManager).Assembly.GetName().Version;
        var latestVersion = await _resourceResolver.GetLatestVersionAsync(owner, repo, cancellationToken).ConfigureAwait(false);

        if (Version.TryParse(latestVersion, out var parsedVersion) &&
            thisVersion != null && parsedVersion > thisVersion)
        {
            var targetUrl = await _resourceResolver.GetReleaseDownloadUrlAsync(owner, repo, cancellationToken).ConfigureAwait(false);
            return targetUrl;
        }
        else
            return default;
    }

    public async Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_updateManager.IsInstalled)
            {
                _logger.LogInformation("Application is not installed via Velopack. Skipping update check.");
                return false;
            }

            _updateInfo = await _updateManager.CheckForUpdatesAsync().ConfigureAwait(false);
            return _updateInfo != null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for updates.");
            return false;
        }
    }

    public async Task DownloadAndApplyUpdatesAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_updateInfo == null)
        {
            _logger.LogWarning("No update info available. Call CheckForUpdatesAsync first.");
            return;
        }

        try
        {
            Action<int>? progressAction = progress != null ? p => progress.Report(p) : null;
            await _updateManager.DownloadUpdatesAsync(_updateInfo, progressAction).ConfigureAwait(false);
            _updateManager.ApplyUpdatesAndRestart(_updateInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download or apply updates.");
            throw;
        }
    }
}
