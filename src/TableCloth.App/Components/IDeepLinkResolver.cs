using System;
using System.Collections.Generic;
using TableCloth.Models;
using TableCloth.Models.Catalog;

namespace TableCloth.Components;

/// <summary>
/// 명령줄(딥링크 포함) 인자를 카탈로그와 대조해 "무엇을 열 것인가"로 바꾼 결과.
/// </summary>
/// <param name="Services">카탈로그에서 확정된 서비스 목록. 비어 있으면 인식 실패다.</param>
/// <param name="ServiceIds">위 서비스들의 카탈로그 정식 Id(대소문자 정규화된 값).</param>
/// <param name="AcceptedTargetUrl">
/// 카탈로그 도메인 게이트를 통과한 대상 주소. 통과하지 못했으면 <see langword="null"/> 이며,
/// 이 경우 샌드박스는 카탈로그의 대표 URL 을 연다.
/// </param>
/// <param name="LaunchImmediately">중간 화면 없이 곧바로 샌드박스를 띄우라는 지시인지 여부.</param>
public sealed record DeepLinkResolution(
    IReadOnlyList<CatalogInternetService> Services,
    IReadOnlyList<string> ServiceIds,
    string? AcceptedTargetUrl,
    bool LaunchImmediately)
{
    /// <summary>카탈로그에서 열 사이트를 하나라도 찾았는지.</summary>
    public bool IsResolved
        => Services.Count > 0;

    /// <summary>
    /// 딥링크가 "지금 바로 띄워라"까지 지시했고 대상도 확정된 상태. 스플래시가 딥링크 모드로
    /// 들어갈지 판단하는 조건이다.
    /// </summary>
    public bool ShouldLaunchImmediately
        => IsResolved && LaunchImmediately;

    public static DeepLinkResolution Unresolved { get; } = new(
        Array.Empty<CatalogInternetService>(),
        Array.Empty<string>(),
        null,
        false);
}

/// <summary>
/// 명령줄/딥링크 인자 → 카탈로그 대상 판정. 최초 실행(스플래시)과 실행 중 인스턴스(파이프 수신)가
/// 같은 판정을 쓰도록 한 곳에 모아둔다.
/// </summary>
public interface IDeepLinkResolver
{
    DeepLinkResolution Resolve(CommandLineArgumentModel? argumentModel);
}
