using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.Configuration;
using Velopack;
using Velopack.Sources;

namespace TableCloth.Components.Implementations;

public sealed class AppUpdateManager : IAppUpdateManager
{
    private readonly ILogger<AppUpdateManager> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPreferencesManager _preferencesManager;
    // 설치 정보(IsInstalled/CurrentVersion)는 채널과 무관하므로 기본 매니저를 유지한다.
    private readonly UpdateManager _updateManager;
    // 채널 인식 매니저 — CheckForUpdates 에서 설정되고 Download/Apply 가 같은 인스턴스를 재사용한다.
    private UpdateManager? _checkUpdateManager;
    private ReleaseChannel _channel = ReleaseChannel.Retail;
    private UpdateInfo? _updateInfo;
    private GitHubReleaseInfo? _latestReleaseInfo;

    private const string Owner = "yourtablecloth";
    private const string Repo = "TableCloth";
    private static string RepoUrl => $"https://github.com/{Owner}/{Repo}";

    public AppUpdateManager(
        ILogger<AppUpdateManager> logger,
        IHttpClientFactory httpClientFactory,
        IPreferencesManager preferencesManager)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _preferencesManager = preferencesManager;
        _updateManager = new UpdateManager(new GithubSource(RepoUrl, null, false));
    }

    // 이슈 #296: 선택된 릴리스 링에 맞는 Velopack 매니저를 만든다.
    private UpdateManager BuildUpdateManager(ReleaseChannel channel)
    {
        var arch = GetCurrentArchitecture();
        var wantPreview = channel == ReleaseChannel.Preview;

        var options = new UpdateOptions
        {
            // 두 링 모두 채널을 명시한다. 비워 두면 Velopack 기본값이 "설치 시점에 구워진 채널"이라,
            // 미리 보기 설치본에서 안정을 골라도 계속 preview-<arch> 채널을 조회해 전환이 되지 않는다
            // (build.cs 의 vpk pack --channel 값과 같은 이름이어야 한다).
            ExplicitChannel = wantPreview ? $"preview-{arch}" : arch,

            // AllowVersionDowngrade 는 켜지 않는다. 앱이 스스로 낮은 버전으로 되돌리는 경로는 두지
            // 않고, 이전 버전으로 가고 싶으면 채널을 안정으로 바꾼 뒤 구 버전 패키지를 직접 받아
            // 설치하도록 안내한다(docs/TROUBLESHOOTING_UPDATE_CHANNEL.md).
        };

        return new UpdateManager(new GithubSource(RepoUrl, null, prerelease: wantPreview), options);
    }

    public bool IsInstalledViaVelopack => _updateManager.IsInstalled;

    public string? CurrentVersion => _updateManager.CurrentVersion?.ToString();

    public string? AvailableVersion => _updateInfo?.TargetFullRelease?.Version?.ToString()
        ?? _latestReleaseInfo?.TagName?.TrimStart('v');

    public Uri GetReleasesPageUrl() => new($"https://github.com/{Owner}/{Repo}/releases");

    public async Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        // 이슈 #296: 사용자가 선택한 릴리스 링을 읽어 채널 인식 매니저를 구성한다(기본 Retail).
        try
        {
            var prefs = await _preferencesManager.LoadPreferencesAsync(cancellationToken).ConfigureAwait(false);
            _channel = prefs?.UpdateChannel ?? ReleaseChannel.Retail;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Cannot read update channel preference; defaulting to Retail.");
            _channel = ReleaseChannel.Retail;
        }

        _checkUpdateManager = BuildUpdateManager(_channel);

        // 먼저 Velopack 표준 방식으로 시도
        try
        {
            _updateInfo = await _checkUpdateManager.CheckForUpdatesAsync().ConfigureAwait(false);

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
        // Velopack 업데이트가 있으면 Velopack 사용(체크에 쓴 채널 인식 매니저를 그대로 사용)
        var velopackManager = _checkUpdateManager ?? _updateManager;
        if (_updateInfo != null)
        {
            try
            {
                _logger.LogInformation("Downloading update via Velopack {Version}...", AvailableVersion);

                Action<int>? progressAction = progress != null ? p => progress.Report(p) : null;
                await velopackManager.DownloadUpdatesAsync(_updateInfo, progressAction).ConfigureAwait(false);

                _logger.LogInformation("Download complete. Applying update and restarting...");
                velopackManager.ApplyUpdatesAndRestart(_updateInfo);
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

            // 파일 이름 규칙(이슈 #296): Retail = TableCloth_{ver}_Release_{arch}.exe,
            // Preview = TableCloth-Preview_{ver}_Release_{arch}.exe. 채널에 맞는 자산만 고른다
            // ("-Preview" 포함 여부로 링 구분 — Retail 이 프리뷰 자산을 잡지 않도록).
            var wantPreview = _channel == ReleaseChannel.Preview;
            var matchingAsset = releaseInfo.Assets.FirstOrDefault(a =>
                a.Name != null &&
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                a.Name.Contains($"_Release_{arch}", StringComparison.OrdinalIgnoreCase) &&
                a.Name.Contains("-Preview", StringComparison.OrdinalIgnoreCase) == wantPreview);

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

    // 이슈 #296: 채널에 따라 GitHub 릴리스 폴백 소스를 분기. Retail 은 /releases/latest(프리릴리스 제외),
    // Preview 는 /releases 목록에서 최신 프리릴리스를 고른다.
    private async Task<GitHubReleaseInfo?> GetLatestReleaseInfoAsync(CancellationToken cancellationToken)
        => _channel == ReleaseChannel.Preview
            ? await GetLatestPreviewReleaseInfoAsync(cancellationToken).ConfigureAwait(false)
            : await GetLatestRetailReleaseInfoAsync(cancellationToken).ConfigureAwait(false);

    private async Task<GitHubReleaseInfo?> GetLatestRetailReleaseInfoAsync(CancellationToken cancellationToken)
    {
        var targetUri = new Uri($"https://api.github.com/repos/{Owner}/{Repo}/releases/latest", UriKind.Absolute);
        var httpClient = _httpClientFactory.CreateGitHubRestApiClient();

        using var response = await httpClient.GetAsync(targetUri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var jsonDocument = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        return ParseReleaseInfo(jsonDocument.RootElement);
    }

    private async Task<GitHubReleaseInfo?> GetLatestPreviewReleaseInfoAsync(CancellationToken cancellationToken)
    {
        // /releases 는 최신순 배열. 첫 프리릴리스를 미리 보기 최신으로 본다.
        var targetUri = new Uri($"https://api.github.com/repos/{Owner}/{Repo}/releases?per_page=30", UriKind.Absolute);
        var httpClient = _httpClientFactory.CreateGitHubRestApiClient();

        using var response = await httpClient.GetAsync(targetUri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var jsonDocument = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (jsonDocument.RootElement.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var releaseElement in jsonDocument.RootElement.EnumerateArray())
        {
            var info = ParseReleaseInfo(releaseElement);
            if (info.Prerelease)
                return info;
        }

        return null;
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
            Prerelease = element.TryGetProperty("prerelease", out var prerelease) && prerelease.ValueKind == JsonValueKind.True,
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
        public bool Prerelease { get; set; }
        public GitHubAssetInfo[]? Assets { get; set; }
    }

    private sealed class GitHubAssetInfo
    {
        public string? Name { get; set; }
        public string? BrowserDownloadUrl { get; set; }
        public long Size { get; set; }
    }
}
