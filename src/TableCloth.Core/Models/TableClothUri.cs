using System;

namespace TableCloth.Models
{
    /// <summary>
    /// <c>tablecloth:</c> 딥링크가 요청하는 대상의 종류.
    /// </summary>
    public enum TableClothUriRequestKind
    {
        /// <summary>해석할 수 없는 요청.</summary>
        Unknown = 0,

        /// <summary><c>tablecloth:wooribank</c> 형태 — 카탈로그 사이트 Id.</summary>
        SiteId,

        /// <summary><c>tablecloth:https://…</c> 형태 — 열어야 할 페이지 주소.</summary>
        TargetUrl,
    }

    /// <summary>
    /// 파싱된 <c>tablecloth:</c> 딥링크 요청.
    /// </summary>
    public sealed class TableClothUriRequest
    {
        private TableClothUriRequest(TableClothUriRequestKind kind, string siteId, string targetUrl)
        {
            Kind = kind;
            SiteId = siteId;
            TargetUrl = targetUrl;
        }

        public TableClothUriRequestKind Kind { get; }

        /// <summary><see cref="TableClothUriRequestKind.SiteId"/>일 때의 사이트 Id.</summary>
        public string SiteId { get; }

        /// <summary><see cref="TableClothUriRequestKind.TargetUrl"/>일 때의 대상 URL(원문 유지).</summary>
        public string TargetUrl { get; }

        internal static TableClothUriRequest ForSiteId(string siteId)
            => new TableClothUriRequest(TableClothUriRequestKind.SiteId, siteId, null);

        internal static TableClothUriRequest ForTargetUrl(string targetUrl)
            => new TableClothUriRequest(TableClothUriRequestKind.TargetUrl, null, targetUrl);

        /// <summary>
        /// 이 요청을 앱이 이미 이해하는 일반 명령줄 인자로 옮긴다.
        /// </summary>
        /// <remarks>
        /// 딥링크 페이로드는 임의의 웹 페이지가 넣는 값이므로 <b>절대 원문 그대로 argv 에 넣지 않는다</b>.
        /// 이 앱의 인자 파이프라인은 <c>@파일</c> 응답 파일 확장을 쓰고 있어(바탕화면 `.tclnk` 연결이
        /// 그 기능에 의존한다), 페이로드가 argv 로 흘러가면 임의 경로(UNC 포함) 파일 읽기가 된다.
        /// 그래서 파서를 통과한 값만 여기서 정규 인자로 다시 만들어 넘긴다.
        /// </remarks>
        public string[] ToCanonicalArguments()
        {
            switch (Kind)
            {
                case TableClothUriRequestKind.SiteId:
                    // 사이트 Id 는 영숫자로 시작하는 제한된 문자 집합만 통과했으므로 위치 인자로 안전하다.
                    return new[] { SiteId, };

                case TableClothUriRequestKind.TargetUrl:
                    return new[] { Resources.ConstantStrings.TableCloth_Switch_TargetUrl, TargetUrl, };

                default:
                    return new string[0];
            }
        }
    }

    /// <summary>
    /// <c>tablecloth:</c> 커스텀 URI 스킴 페이로드 파서.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 지원 형태:
    /// <list type="bullet">
    /// <item><c>tablecloth:wooribank</c> — 카탈로그 사이트 Id(대소문자 무시, 해석은 호출자 몫).</item>
    /// <item><c>tablecloth:https://spib.wooribank.com/…</c> — 열어야 할 페이지 주소.</item>
    /// </list>
    /// <c>tablecloth://…</c> 처럼 슬래시 두 개가 붙은 형태도 같은 의미로 받는다(브라우저·사용자가 흔히
    /// 그렇게 적는다).
    /// </para>
    /// <para>
    /// <b>URL 은 퍼센트 디코딩하지 않는다.</b> 생산자가 이미 인코딩한 URL(예: 한글 질의문자열의
    /// <c>%EC%9A%B0</c>)을 여기서 한 번 더 디코딩하면 원래 주소가 깨진다. 스킴만 떼고 나머지는
    /// 원문 그대로 다음 단계(<see cref="Catalog.CatalogTargetUrlMatcher"/>)로 넘긴다.
    /// </para>
    /// </remarks>
    public static class TableClothUri
    {
        /// <summary>등록하는 URI 스킴 이름.</summary>
        public const string SchemeName = "tablecloth";

        /// <summary>페이로드 앞에 붙는 스킴 접두사.</summary>
        public const string SchemePrefix = SchemeName + ":";

        /// <summary>허용하는 전체 딥링크 길이(스킴 포함).</summary>
        public const int MaxLength = 2100;

        /// <summary>사이트 Id 로 허용하는 최대 길이.</summary>
        private const int MaxSiteIdLength = 64;

        private const string HttpPrefix = "http://";
        private const string HttpsPrefix = "https://";

        /// <summary>
        /// 이 문자열이 <c>tablecloth:</c> 딥링크로 보이는지 여부(내용 유효성은 보지 않는다).
        /// </summary>
        public static bool IsDeepLink(string value)
            => !string.IsNullOrWhiteSpace(value)
               && value.Trim().StartsWith(SchemePrefix, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// <c>tablecloth:</c> 딥링크를 파싱한다.
        /// </summary>
        /// <param name="value">셸이 넘긴 원문(신뢰할 수 없는 입력).</param>
        /// <param name="request">성공 시 파싱 결과.</param>
        public static bool TryParse(string value, out TableClothUriRequest request)
        {
            request = null;

            if (!IsDeepLink(value))
                return false;

            var trimmed = value.Trim();

            if (trimmed.Length > MaxLength)
                return false;

            var payload = trimmed.Substring(SchemePrefix.Length);

            // `tablecloth://wooribank` / `tablecloth://https://…` 형태 허용.
            if (payload.StartsWith("//", StringComparison.Ordinal))
                payload = payload.Substring(2);

            if (string.IsNullOrWhiteSpace(payload))
                return false;

            if (payload.StartsWith(HttpPrefix, StringComparison.OrdinalIgnoreCase) ||
                payload.StartsWith(HttpsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // 세부 검증(스킴/자격증명/불안전 문자/카탈로그 도메인)은 CatalogTargetUrlMatcher 가 한다.
                request = TableClothUriRequest.ForTargetUrl(payload);
                return true;
            }

            // 사이트 Id 형태. 브라우저가 붙이는 후행 슬래시는 떼고 본다.
            var siteId = payload.TrimEnd('/');

            if (!IsSafeSiteIdToken(siteId))
                return false;

            request = TableClothUriRequest.ForSiteId(siteId);
            return true;
        }

        /// <summary>
        /// 위치 인자로 그대로 넘겨도 안전한 사이트 Id 토큰인지 검사한다.
        /// </summary>
        /// <remarks>
        /// 첫 글자를 영숫자로 강제하는 것이 핵심이다. <c>@</c> 로 시작하면 응답 파일(임의 경로 읽기),
        /// <c>-</c>/<c>--</c> 로 시작하면 스위치로 해석되기 때문이다. 카탈로그의 실제 Id 는 모두
        /// 영숫자로 시작한다.
        /// </remarks>
        private static bool IsSafeSiteIdToken(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaxSiteIdLength)
                return false;

            if (!IsAsciiLetterOrDigit(value[0]))
                return false;

            for (var i = 1; i < value.Length; i++)
            {
                var ch = value[i];

                if (IsAsciiLetterOrDigit(ch) || ch == '.' || ch == '_' || ch == '-')
                    continue;

                return false;
            }

            return true;
        }

        private static bool IsAsciiLetterOrDigit(char ch)
            => (ch >= 'a' && ch <= 'z')
               || (ch >= 'A' && ch <= 'Z')
               || (ch >= '0' && ch <= '9');
    }
}
