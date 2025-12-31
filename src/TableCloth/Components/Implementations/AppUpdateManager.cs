using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace TableCloth.Components.Implementations;

public sealed class AppUpdateManager : IAppUpdateManager
{
    private readonly ILogger<AppUpdateManager> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UpdateManager _updateManager;
    private UpdateInfo? _updateInfo;
    private GitHubReleaseInfo? _latestReleaseInfo;

    private const string Owner = "yourtablecloth";
    private const string Repo = "TableCloth";

    public AppUpdateManager(
        ILogger<AppUpdateManager> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _updateManager = new UpdateManager(
            new GithubSource($"https://github.com/{Owner}/{Repo}", null, false));
    }

    public bool IsInstalledViaVelopack => _updateManager.IsInstalled;

    public string? CurrentVersion => _updateManager.CurrentVersion?.ToString();

    public string? AvailableVersion => _updateInfo?.TargetFullRelease?.Version?.ToString()
        ?? _latestReleaseInfo?.TagName?.TrimStart('v');

    public Uri GetReleasesPageUrl() => new($"https://github.com/{Owner}/{Repo}/releases");

    public async Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        // 먼저 Velopack 표준 방식으로 시도
        try
        {
            _updateInfo = await _updateManager.CheckForUpdatesAsync().ConfigureAwait(false);

            if (_updateInfo != null)
            {
                _logger.LogInformation(
                    "Update available (Velopack): {CurrentVersion} -> {NewVersion}",
                    CurrentVersion,
                    AvailableVersion);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Velopack standard update check failed, trying GitHub API fallback.");
        }

        // Velopack 실패 시 GitHub API로 커스텀 파일 이름 규칙 확인
        try
        {
            _latestReleaseInfo = await GetLatestReleaseInfoAsync(cancellationToken).ConfigureAwait(false);

            if (_latestReleaseInfo != null)
            {
                var latestVersion = _latestReleaseInfo.TagName?.TrimStart('v');
                var currentVersion = CurrentVersion ?? Helpers.GetAppVersion();

                if (!string.IsNullOrEmpty(latestVersion) &&
                    IsNewerVersion(latestVersion, currentVersion))
                {
                    _logger.LogInformation(
                        "Update available (GitHub): {CurrentVersion} -> {NewVersion}",
                        currentVersion,
                        latestVersion);
                    return true;
                }
            }

            _logger.LogInformation("No updates available. Current version: {CurrentVersion}", CurrentVersion);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for updates via GitHub API.");
            return false;
        }
    }

    public async Task DownloadAndApplyUpdatesAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        // Velopack 업데이트가 있으면 Velopack 사용
        if (_updateInfo != null)
        {
            try
            {
                _logger.LogInformation("Downloading update via Velopack {Version}...", AvailableVersion);

                Action<int>? progressAction = progress != null ? p => progress.Report(p) : null;
                await _updateManager.DownloadUpdatesAsync(_updateInfo, progressAction).ConfigureAwait(false);

                _logger.LogInformation("Download complete. Applying update and restarting...");
                _updateManager.ApplyUpdatesAndRestart(_updateInfo);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download or apply updates via Velopack.");
                throw;
            }
        }

        // GitHub 릴리스로 폴백 - 브라우저에서 다운로드 페이지 열기
        var downloadUrl = await GetLatestReleaseDownloadUrlAsync(cancellationToken).ConfigureAwait(false);
        if (downloadUrl != null)
        {
            _logger.LogInformation("Opening download URL: {Url}", downloadUrl);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = downloadUrl.AbsoluteUri,
                UseShellExecute = true
            });
        }
        else
        {
            // 다운로드 URL을 찾지 못하면 릴리스 페이지 열기
            _logger.LogInformation("Opening releases page.");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = GetReleasesPageUrl().AbsoluteUri,
                UseShellExecute = true
            });
        }
    }

    public async Task<Uri?> GetLatestReleaseDownloadUrlAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var releaseInfo = _latestReleaseInfo ?? await GetLatestReleaseInfoAsync(cancellationToken).ConfigureAwait(false);

            if (releaseInfo?.Assets == null || releaseInfo.Assets.Length == 0)
                return null;

            // 현재 아키텍처 확인
            var arch = GetCurrentArchitecture();

            // 파일 이름 규칙: TableCloth_{version}_Release_{arch}.exe
            // 예: TableCloth_1.15.0.0_Release_x64.exe, TableCloth_1.15.0.0_Release_arm64.exe
            var matchingAsset = releaseInfo.Assets.FirstOrDefault(a =>
                a.Name != null &&
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                a.Name.Contains($"_Release_{arch}", StringComparison.OrdinalIgnoreCase));

            if (matchingAsset?.BrowserDownloadUrl != null &&
                Uri.TryCreate(matchingAsset.BrowserDownloadUrl, UriKind.Absolute, out var downloadUri))
            {
                _logger.LogInformation("Found matching release asset: {AssetName}", matchingAsset.Name);
                return downloadUri;
            }

            // 아키텍처별 파일을 찾지 못하면 첫 번째 exe 파일 사용
            var fallbackAsset = releaseInfo.Assets.FirstOrDefault(a =>
                a.Name != null &&
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

            if (fallbackAsset?.BrowserDownloadUrl != null &&
                Uri.TryCreate(fallbackAsset.BrowserDownloadUrl, UriKind.Absolute, out var fallbackUri))
            {
                _logger.LogInformation("Using fallback release asset: {AssetName}", fallbackAsset.Name);
                return fallbackUri;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get latest release download URL.");
            return null;
        }
    }

    private async Task<GitHubReleaseInfo?> GetLatestReleaseInfoAsync(CancellationToken cancellationToken)
    {
        var targetUri = new Uri($"https://api.github.com/repos/{Owner}/{Repo}/releases/latest", UriKind.Absolute);
        var httpClient = _httpClientFactory.CreateGitHubRestApiClient();

        using var response = await httpClient.GetAsync(targetUri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var jsonDocument = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        return ParseReleaseInfo(jsonDocument.RootElement);
    }

    private static GitHubReleaseInfo ParseReleaseInfo(JsonElement element)
    {
        var assets = element.TryGetProperty("assets", out var assetsElement) && assetsElement.ValueKind == JsonValueKind.Array
            ? assetsElement.EnumerateArray()
                .Select(a => new GitHubAssetInfo
                {
                    Name = a.TryGetProperty("name", out var name) ? name.GetString() : null,
                    BrowserDownloadUrl = a.TryGetProperty("browser_download_url", out var url) ? url.GetString() : null,
                    Size = a.TryGetProperty("size", out var size) ? size.GetInt64() : 0
                })
                .ToArray()
            : Array.Empty<GitHubAssetInfo>();

        return new GitHubReleaseInfo
        {
            TagName = element.TryGetProperty("tag_name", out var tagName) ? tagName.GetString() : null,
            Name = element.TryGetProperty("name", out var name) ? name.GetString() : null,
            HtmlUrl = element.TryGetProperty("html_url", out var htmlUrl) ? htmlUrl.GetString() : null,
            Assets = assets
        };
    }

    private static string GetCurrentArchitecture()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            _ => "x64" // 기본값
        };
    }

    private static bool IsNewerVersion(string latestVersion, string currentVersion)
    {
        if (Version.TryParse(latestVersion, out var latest) &&
            Version.TryParse(currentVersion, out var current))
        {
            return latest > current;
        }

        // 버전 파싱 실패 시 문자열 비교
        return string.Compare(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private sealed class GitHubReleaseInfo
    {
        public string? TagName { get; set; }
        public string? Name { get; set; }
        public string? HtmlUrl { get; set; }
        public GitHubAssetInfo[]? Assets { get; set; }
    }

    private sealed class GitHubAssetInfo
    {
        public string? Name { get; set; }
        public string? BrowserDownloadUrl { get; set; }
        public long Size { get; set; }
    }
}
