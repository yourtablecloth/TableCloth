using System;
using System.Threading;
using System.Threading.Tasks;

namespace TableCloth.Components;

public interface IAppUpdateManager
{
    /// <summary>
    /// Velopack을 통해 설치된 앱인지 확인
    /// </summary>
    bool IsInstalledViaVelopack { get; }

    /// <summary>
    /// 현재 설치된 앱 버전
    /// </summary>
    string? CurrentVersion { get; }

    /// <summary>
    /// 업데이트 가능한 새 버전 (CheckForUpdatesAsync 호출 후 사용 가능)
    /// </summary>
    string? AvailableVersion { get; }

    /// <summary>
    /// 업데이트가 있는지 확인
    /// </summary>
    Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 업데이트 다운로드 및 적용 (앱 재시작됨)
    /// </summary>
    Task DownloadAndApplyUpdatesAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// GitHub Releases 페이지 URL 반환
    /// </summary>
    Uri GetReleasesPageUrl();

    /// <summary>
    /// 현재 아키텍처에 맞는 최신 릴리스 다운로드 URL 반환
    /// </summary>
    /// <returns>다운로드 URL, 찾지 못한 경우 null</returns>
    Task<Uri?> GetLatestReleaseDownloadUrlAsync(CancellationToken cancellationToken = default);
}
