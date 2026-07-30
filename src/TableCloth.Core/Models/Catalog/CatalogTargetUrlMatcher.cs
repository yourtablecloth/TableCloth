using System;
using System.Collections.Generic;
using System.Linq;

namespace TableCloth.Models.Catalog
{
    /// <summary>
    /// 외부에서 전달된 대상 URL 이 거부된 이유.
    /// </summary>
    public enum CatalogTargetUrlRejectionReason
    {
        /// <summary>거부되지 않음(수락).</summary>
        None = 0,

        /// <summary>URL 이 지정되지 않았거나 치환되지 않은 플레이스홀더가 그대로 들어옴.</summary>
        NotSpecified,

        /// <summary>절대 URL 로 파싱되지 않음.</summary>
        Malformed,

        /// <summary>http / https 이외의 스킴.</summary>
        UnsupportedScheme,

        /// <summary>`https://bank.example.com@evil.example/` 형태의 자격 증명 삽입.</summary>
        EmbeddedCredentials,

        /// <summary>허용 길이 초과.</summary>
        TooLong,

        /// <summary>공백/제어문자/큰따옴표 등 하위 실행 경로를 오염시킬 수 있는 문자 포함.</summary>
        UnsafeCharacters,

        /// <summary>카탈로그의 어떤 서비스와도 등록 도메인이 일치하지 않음.</summary>
        NoCatalogDomainMatch,

        /// <summary>같은 등록 도메인에 여러 서비스가 있어 어느 것을 설치할지 확정할 수 없음.</summary>
        AmbiguousCandidates,
    }

    /// <summary>
    /// <see cref="CatalogTargetUrlMatcher.Match"/> 의 판정 결과.
    /// </summary>
    public sealed class CatalogTargetUrlMatchResult
    {
        private CatalogTargetUrlMatchResult(
            bool isAccepted, string acceptedUrl,
            IReadOnlyList<string> serviceIds,
            CatalogTargetUrlRejectionReason reason)
        {
            IsAccepted = isAccepted;
            AcceptedUrl = acceptedUrl;
            ServiceIds = serviceIds ?? new string[0];
            Reason = reason;
        }

        /// <summary>URL 을 열어도 되는지 여부.</summary>
        public bool IsAccepted { get; }

        /// <summary>
        /// 수락된 경우 실제로 열어야 할 URL. 정규화하지 않고 전달된 원문을 그대로 돌려준다
        /// (은행 사이트의 퍼센트 인코딩된 쿼리 파라미터가 재정규화로 의미가 바뀌는 일을 피한다).
        /// </summary>
        public string AcceptedUrl { get; }

        /// <summary>수락된 경우 설치 단계를 조립할 카탈로그 서비스 Id 목록.</summary>
        public IReadOnlyList<string> ServiceIds { get; }

        /// <summary>거부 사유. 수락 시 <see cref="CatalogTargetUrlRejectionReason.None"/>.</summary>
        public CatalogTargetUrlRejectionReason Reason { get; }

        internal static CatalogTargetUrlMatchResult Accept(string acceptedUrl, IReadOnlyList<string> serviceIds)
            => new CatalogTargetUrlMatchResult(true, acceptedUrl, serviceIds, CatalogTargetUrlRejectionReason.None);

        internal static CatalogTargetUrlMatchResult Reject(CatalogTargetUrlRejectionReason reason)
            => new CatalogTargetUrlMatchResult(false, null, null, reason);
    }

    /// <summary>
    /// 외부(무설치 `.wsb` 딥링크, 브라우저 익스텐션)에서 전달된 임의 URL 이 카탈로그가 아는
    /// 도메인에 속하는지 판정하는 게이트.
    /// </summary>
    /// <remarks>
    /// <para>
    /// `.wsb` 는 신뢰 경계 밖에서 배포되므로 여기 들어오는 URL 과 사이트 Id 는 모두 신뢰할 수 없는
    /// 입력이다. 이 게이트가 유일한 방어선이다.
    /// </para>
    /// <para>
    /// 판정은 <b>퍼블릭 서픽스를 인식한 라벨 단위 등록 도메인 일치</b>다. 문자열
    /// <c>EndsWith</c> / <c>Contains</c> 는 절대 쓰지 않는다 — 라벨 경계가 없으면
    /// <c>evilwooribank.com</c> 이 <c>wooribank.com</c> 으로 통과한다.
    /// </para>
    /// <para>
    /// 동점 처리: 여러 서비스가 한 등록 도메인을 공유하는 경우(예: 우리은행 개인/기업, 하나은행
    /// 개인/기업/저축은행, <c>fsb.or.kr</c> 아래 저축은행 25곳)에는 어느 쪽 패키지를 설치할지
    /// 자동 결정할 수 없다. 그래서 <b>생산자가 사이트 Id 를 같이 넘기는 것을 정상 경로로 본다</b>
    /// (익스텐션 팝업이 사용자에게 고르게 한다). Id 없이 URL 만 온 경우에는 호스트 라벨이 더 많이
    /// 일치하는 유일 후보만 수락하고, 동점이면 URL 을 버린다.
    /// </para>
    /// </remarks>
    public static class CatalogTargetUrlMatcher
    {
        /// <summary>허용하는 URL 최대 길이.</summary>
        public const int MaxTargetUrlLength = 2048;

        // 무설치 런처(BootstrapOptions.Meaningful)와 같은 규칙: 치환되지 않은 플레이스홀더는 "없음"으로 본다.
        private const string PlaceholderMarker = "__SPORK";

        private static readonly char[] LabelSeparator = new char[] { '.', };

        /// <summary>
        /// 등록 도메인 계산에 쓰는 다중 라벨 퍼블릭 서픽스 목록(선별 목록, 전체 PSL 아님).
        /// </summary>
        /// <remarks>
        /// 이 목록이 없으면 "끝 두 라벨 = 등록 도메인" 계산이 <c>www.ibk.co.kr</c> 을 <c>co.kr</c> 로
        /// 만들어 <b>모든 .co.kr 사이트가 서로 일치</b>하게 된다. 카탈로그 서비스 상당수가 .co.kr /
        /// .or.kr / .go.kr 이므로 최소한 kr 2단계 목록은 필수다. 목록에 없는 다중 라벨 서픽스는
        /// <see cref="GenericSecondLevelLabels"/> 가드가 2차로 막는다.
        /// </remarks>
        private static readonly HashSet<string> MultiLabelPublicSuffixes = new HashSet<string>(StringComparer.Ordinal)
        {
            // .kr 2단계
            "co.kr", "or.kr", "go.kr", "ac.kr", "re.kr", "ne.kr", "pe.kr",
            "mil.kr", "hs.kr", "ms.kr", "es.kr", "sc.kr", "kg.kr",

            // .kr 지역 도메인
            "seoul.kr", "busan.kr", "daegu.kr", "incheon.kr", "gwangju.kr", "daejeon.kr",
            "ulsan.kr", "sejong.kr", "gyeonggi.kr", "gangwon.kr", "chungbuk.kr", "chungnam.kr",
            "jeonbuk.kr", "jeonnam.kr", "gyeongbuk.kr", "gyeongnam.kr", "jeju.kr",

            // 해외 서비스가 카탈로그에 들어올 때를 대비한 최소 목록
            "co.jp", "co.uk", "org.uk", "com.au", "co.nz",
            "com.cn", "com.tw", "co.in", "com.br", "com.hk", "com.sg",
        };

        /// <summary>
        /// 등록 도메인의 최상위 라벨로 올 수 없는 일반 토큰. 서픽스 목록이 불완전할 때의 과다 일치를 막는다.
        /// </summary>
        /// <remarks>
        /// 예: <c>co.za</c> 가 위 목록에 없으면 <c>www.a.co.za</c> 의 등록 도메인이 <c>co.za</c> 로
        /// 계산되어 무관한 <c>b.co.za</c> 와 일치해 버린다. 등록 도메인의 첫 라벨이 이 목록에 있으면
        /// 계산 자체를 실패로 처리한다.
        /// </remarks>
        private static readonly HashSet<string> GenericSecondLevelLabels = new HashSet<string>(StringComparer.Ordinal)
        {
            "co", "or", "go", "ac", "re", "ne", "pe",
            "com", "net", "org", "gov", "edu", "mil", "biz", "info",
        };

        /// <summary>
        /// 전달된 URL 이 카탈로그 도메인에 속하는지 판정한다.
        /// </summary>
        /// <param name="catalog">현재 카탈로그.</param>
        /// <param name="targetUrl">외부에서 전달된 URL(신뢰할 수 없는 입력).</param>
        /// <param name="preselectedSiteIds">
        /// 생산자가 같이 넘긴 카탈로그 사이트 Id 목록(선택). 넘어온 경우 <b>모든</b> Id 가 URL 과 같은
        /// 등록 도메인이어야 수락한다 — URL 과 무관한 Id 를 끼워 제3자 보안 프로그램 설치를
        /// 편승시키는 `.wsb` 를 막는다.
        /// </param>
        public static CatalogTargetUrlMatchResult Match(
            CatalogDocument catalog, string targetUrl, IEnumerable<string> preselectedSiteIds = null)
        {
            var validation = ValidateUrl(targetUrl, out var targetHost);

            if (validation != CatalogTargetUrlRejectionReason.None)
                return CatalogTargetUrlMatchResult.Reject(validation);

            if (!TryGetRegistrableDomain(targetHost, out var targetDomain))
                return CatalogTargetUrlMatchResult.Reject(CatalogTargetUrlRejectionReason.NoCatalogDomainMatch);

            var services = (catalog?.Services ?? new List<CatalogInternetService>())
                .Select(service => new
                {
                    Service = service,
                    Host = TryGetHost(service?.Url),
                })
                .Where(x => x.Service != null && !string.IsNullOrEmpty(x.Host))
                .ToList();

            var acceptedUrl = targetUrl.Trim();

            var requestedIds = (preselectedSiteIds ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id) && id.IndexOf(PlaceholderMarker, StringComparison.Ordinal) < 0)
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var requestedServices = requestedIds
                .Select(id => services.FirstOrDefault(x => string.Equals(x.Service.Id, id, StringComparison.Ordinal)))
                .Where(x => x != null)
                .ToList();

            // (1) 생산자가 Id 를 넘긴 정상 경로: 넘어온 Id 전부가 URL 의 등록 도메인과 일치해야 한다.
            if (requestedServices.Count > 0)
            {
                var allWithinDomain = requestedServices.All(x =>
                    TryGetRegistrableDomain(x.Host, out var domain) &&
                    string.Equals(domain, targetDomain, StringComparison.Ordinal));

                if (!allWithinDomain)
                    return CatalogTargetUrlMatchResult.Reject(CatalogTargetUrlRejectionReason.NoCatalogDomainMatch);

                return CatalogTargetUrlMatchResult.Accept(
                    acceptedUrl,
                    requestedServices.Select(x => x.Service.Id).ToList());
            }

            // (2) URL 만 온 경로: 같은 등록 도메인 후보 중 호스트 라벨이 가장 많이 일치하는 유일 후보만 수락.
            var candidates = services
                .Where(x => TryGetRegistrableDomain(x.Host, out var domain) &&
                            string.Equals(domain, targetDomain, StringComparison.Ordinal))
                .Select(x => new
                {
                    x.Service,
                    Score = CountSharedLabelsBeyondDomain(targetHost, x.Host, targetDomain),
                })
                .ToList();

            if (candidates.Count < 1)
                return CatalogTargetUrlMatchResult.Reject(CatalogTargetUrlRejectionReason.NoCatalogDomainMatch);

            var bestScore = candidates.Max(x => x.Score);
            var winners = candidates.Where(x => x.Score == bestScore).ToList();

            if (winners.Count != 1)
                return CatalogTargetUrlMatchResult.Reject(CatalogTargetUrlRejectionReason.AmbiguousCandidates);

            return CatalogTargetUrlMatchResult.Accept(
                acceptedUrl,
                new[] { winners[0].Service.Id });
        }

        /// <summary>
        /// 호스트에서 등록 도메인(퍼블릭 서픽스 + 라벨 1개)을 계산한다.
        /// </summary>
        /// <remarks>
        /// 호스트가 곧 퍼블릭 서픽스이거나, 서픽스 바로 위 라벨이 일반 토큰(co / or / com …)이면
        /// 실패로 처리한다. IP 리터럴도 라벨 규칙에 걸려 자연히 실패한다.
        /// </remarks>
        public static bool TryGetRegistrableDomain(string host, out string registrableDomain)
        {
            registrableDomain = null;

            if (string.IsNullOrWhiteSpace(host))
                return false;

            var labels = NormalizeHost(host).Split(LabelSeparator, StringSplitOptions.None);

            if (labels.Length < 2 || labels.Any(label => label.Length < 1))
                return false;

            var suffixLength = GetPublicSuffixLength(labels);

            if (labels.Length <= suffixLength)
                return false;

            var domainLabels = labels.Skip(labels.Length - suffixLength - 1).ToArray();

            if (GenericSecondLevelLabels.Contains(domainLabels[0]))
                return false;

            registrableDomain = string.Join(".", domainLabels);
            return true;
        }

        private static CatalogTargetUrlRejectionReason ValidateUrl(string targetUrl, out string host)
        {
            host = null;

            if (string.IsNullOrWhiteSpace(targetUrl) ||
                targetUrl.IndexOf(PlaceholderMarker, StringComparison.Ordinal) > (-1))
                return CatalogTargetUrlRejectionReason.NotSpecified;

            var trimmed = targetUrl.Trim();

            if (trimmed.Length > MaxTargetUrlLength)
                return CatalogTargetUrlRejectionReason.TooLong;

            // 공백/제어문자/큰따옴표는 정상 URL 에 나타나지 않으며, 하위 브라우저 실행 인자를
            // 오염시킬 수 있으므로 여기서 끊는다. (실행 경로도 ArgumentList 를 쓰지만 이중 방어.)
            if (trimmed.Any(ch => ch <= ' ' || ch == '\u007F' || ch == '\u0022'))
                return CatalogTargetUrlRejectionReason.UnsafeCharacters;

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
                return CatalogTargetUrlRejectionReason.Malformed;

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return CatalogTargetUrlRejectionReason.UnsupportedScheme;

            // `https://www.wooribank.com@evil.example/` 처럼 사용자 눈속임용 자격 증명이 붙은 URL 은
            // Uri 가 호스트를 evil.example 로 올바르게 파싱하지만, 사용자에게 URL 을 보여줄 때
            // 혼동을 주므로 아예 받지 않는다.
            if (!string.IsNullOrEmpty(uri.UserInfo))
                return CatalogTargetUrlRejectionReason.EmbeddedCredentials;

            // IdnHost 는 국제화 도메인을 punycode 로 정규화해 돌려준다(카탈로그 호스트와 같은 표현).
            host = NormalizeHost(uri.IdnHost);

            if (string.IsNullOrEmpty(host))
                return CatalogTargetUrlRejectionReason.Malformed;

            return CatalogTargetUrlRejectionReason.None;
        }

        private static string TryGetHost(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
                return null;

            return NormalizeHost(uri.IdnHost);
        }

        private static string NormalizeHost(string host)
            => (host ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();

        private static int GetPublicSuffixLength(string[] labels)
        {
            // 다중 라벨 서픽스를 긴 것부터 확인하고, 해당 없으면 마지막 라벨 하나를 서픽스로 본다.
            var maxSuffixLength = Math.Min(labels.Length - 1, 3);

            for (var length = maxSuffixLength; length >= 2; length--)
            {
                var suffix = string.Join(".", labels.Skip(labels.Length - length));

                if (MultiLabelPublicSuffixes.Contains(suffix))
                    return length;
            }

            return 1;
        }

        /// <summary>
        /// 등록 도메인보다 위쪽(왼쪽) 라벨이 몇 개까지 연속으로 일치하는지 센다. 순위 결정용.
        /// </summary>
        /// <remarks>
        /// <c>ok.ibs.fsb.or.kr</c> 입력은 같은 호스트(3점: ok.ibs.fsb)가 <c>jt.ibs.fsb.or.kr</c>(1점: ibs)
        /// 를 앞질러 유일 승자가 된다. 반면 <c>spib.wooribank.com</c> 은 <c>www.wooribank.com</c> /
        /// <c>nbi.wooribank.com</c> 양쪽 모두 0점이라 동점이 되고, 이 경우 URL 은 수락되지 않는다.
        /// </remarks>
        private static int CountSharedLabelsBeyondDomain(string targetHost, string serviceHost, string registrableDomain)
        {
            var domainLabelCount = registrableDomain.Split(LabelSeparator, StringSplitOptions.None).Length;
            var targetLabels = targetHost.Split(LabelSeparator, StringSplitOptions.None);
            var serviceLabels = serviceHost.Split(LabelSeparator, StringSplitOptions.None);

            var shared = 0;

            for (var offset = domainLabelCount; ; offset++)
            {
                var targetIndex = targetLabels.Length - offset - 1;
                var serviceIndex = serviceLabels.Length - offset - 1;

                if (targetIndex < 0 || serviceIndex < 0)
                    break;

                if (!string.Equals(targetLabels[targetIndex], serviceLabels[serviceIndex], StringComparison.Ordinal))
                    break;

                shared++;
            }

            return shared;
        }
    }
}
