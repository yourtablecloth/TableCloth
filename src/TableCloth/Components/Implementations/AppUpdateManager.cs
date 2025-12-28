using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace TableCloth.Components.Implementations;

public sealed class AppUpdateManager : IAppUpdateManager
{
    private readonly ILogger<AppUpdateManager> _logger;
    private readonly UpdateManager _updateManager;
    private UpdateInfo? _updateInfo;

    private const string Owner = "yourtablecloth";
    private const string Repo = "TableCloth";

    public AppUpdateManager(ILogger<AppUpdateManager> logger)
    {
        _logger = logger;
        _updateManager = new UpdateManager(
            new GithubSource($"https://github.com/{Owner}/{Repo}", null, false));
    }

    public bool IsInstalledViaVelopack => _updateManager.IsInstalled;

    public string? CurrentVersion => _updateManager.CurrentVersion?.ToString();

    public string? AvailableVersion => _updateInfo?.TargetFullRelease?.Version?.ToString();

    public Uri GetReleasesPageUrl() => new($"https://github.com/{Owner}/{Repo}/releases");

    public async Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _updateInfo = await _updateManager.CheckForUpdatesAsync().ConfigureAwait(false);

            if (_updateInfo != null)
            {
                _logger.LogInformation(
                    "Update available: {CurrentVersion} -> {NewVersion}",
                    CurrentVersion,
                    AvailableVersion);
            }
            else
            {
                _logger.LogInformation("No updates available. Current version: {CurrentVersion}", CurrentVersion);
            }

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
            _logger.LogInformation("Downloading update {Version}...", AvailableVersion);

            Action<int>? progressAction = progress != null ? p => progress.Report(p) : null;
            await _updateManager.DownloadUpdatesAsync(_updateInfo, progressAction).ConfigureAwait(false);

            _logger.LogInformation("Download complete. Applying update and restarting...");
            _updateManager.ApplyUpdatesAndRestart(_updateInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download or apply updates.");
            throw;
        }
    }
}
