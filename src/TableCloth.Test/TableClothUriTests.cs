using TableCloth.Models;

namespace TableCloth.Test
{
    /// <summary>
    /// <c>tablecloth:</c> 커스텀 URI 스킴 페이로드 파서(<see cref="TableClothUri"/>) 검증.
    /// </summary>
    /// <remarks>
    /// 이 페이로드는 <b>임의의 웹 페이지</b>가 넣는 값이라 신뢰할 수 없다. 특히 이 앱의 인자 파이프라인은
    /// <c>@파일</c> 응답 파일 확장을 쓰고 있어(바탕화면 `.tclnk` 연결이 그 기능에 의존한다), 페이로드가
    /// 위치 인자로 흘러가면 임의 경로 파일 읽기가 된다. 그래서 사이트 Id 토큰의 문자 집합 검사가
    /// 여기서 가장 중요한 테스트다.
    /// </remarks>
    [TestClass]
    public sealed class TableClothUriTests
    {
        [TestMethod]
        public void SiteIdForm_IsParsed()
        {
            Assert.IsTrue(TableClothUri.TryParse("tablecloth:wooribank", out var request));
            Assert.AreEqual(TableClothUriRequestKind.SiteId, request.Kind);
            Assert.AreEqual("wooribank", request.SiteId);

            // 딥링크는 항상 --launch 를 동반한다(상세 화면 없이 곧바로 샌드박스 실행).
            // 사이트 Id 자체는 위치 인자로 넘어간다(스위치가 아니다).
            CollectionAssert.AreEqual(new[] { "--launch", "wooribank" }, request.ToCanonicalArguments());
        }

        [TestMethod]
        public void TargetUrlForm_IsParsed_AndKeptVerbatim()
        {
            // 퍼센트 인코딩된 값이 그대로 보존돼야 한다(여기서 한 번 더 디코딩하면 주소가 깨진다).
            const string url = "https://spib.wooribank.com/pib/Dream?withyou=CTCER0149&q=%EC%9A%B0%EB%A6%AC";

            Assert.IsTrue(TableClothUri.TryParse("tablecloth:" + url, out var request));
            Assert.AreEqual(TableClothUriRequestKind.TargetUrl, request.Kind);
            Assert.AreEqual(url, request.TargetUrl);

            CollectionAssert.AreEqual(new[] { "--launch", "--target-url", url }, request.ToCanonicalArguments());
        }

        [TestMethod]
        public void DoubleSlashForm_IsAccepted_ForBothShapes()
        {
            Assert.IsTrue(TableClothUri.TryParse("tablecloth://wooribank", out var siteRequest));
            Assert.AreEqual("wooribank", siteRequest.SiteId);

            Assert.IsTrue(TableClothUri.TryParse("tablecloth://https://www.wooribank.com/", out var urlRequest));
            Assert.AreEqual("https://www.wooribank.com/", urlRequest.TargetUrl);
        }

        [TestMethod]
        public void SchemeName_IsCaseInsensitive_AndTrailingSlashIsTrimmed()
        {
            Assert.IsTrue(TableClothUri.TryParse("TableCloth:WooriBank/", out var request));
            Assert.AreEqual(TableClothUriRequestKind.SiteId, request.Kind);
            Assert.AreEqual("WooriBank", request.SiteId);
        }

        [TestMethod]
        public void ResponseFileToken_IsRejected()
        {
            // `@` 로 시작하면 응답 파일로 해석돼 임의 경로(UNC 포함) 읽기가 된다.
            Assert.IsFalse(TableClothUri.TryParse(@"tablecloth:@\\attacker.example\share\payload.txt", out _));
            Assert.IsFalse(TableClothUri.TryParse("tablecloth:@C:/Windows/win.ini", out _));
        }

        [TestMethod]
        public void SwitchLookingToken_IsRejected()
        {
            // `-`/`--` 로 시작하면 스위치로 해석된다.
            Assert.IsFalse(TableClothUri.TryParse("tablecloth:--cert-private-key", out _));
            Assert.IsFalse(TableClothUri.TryParse("tablecloth:-h", out _));
        }

        [TestMethod]
        public void SiteIdWithSeparatorsOrSpaces_IsRejected()
        {
            // 경로/질의/공백이 섞인 토큰은 사이트 Id 가 아니다.
            Assert.IsFalse(TableClothUri.TryParse("tablecloth:woori bank", out _));
            Assert.IsFalse(TableClothUri.TryParse("tablecloth:woori/bank", out _));
            Assert.IsFalse(TableClothUri.TryParse("tablecloth:woori?bank", out _));
            Assert.IsFalse(TableClothUri.TryParse("tablecloth:woori\"bank", out _));
        }

        [TestMethod]
        public void NonHttpNestedScheme_IsNotTreatedAsUrl()
        {
            // file:/javascript: 등은 URL 형식으로 받지 않는다. 사이트 Id 문자 집합에도 걸려 거부된다.
            Assert.IsFalse(TableClothUri.TryParse("tablecloth:file:///C:/Windows/System32/calc.exe", out _));
            Assert.IsFalse(TableClothUri.TryParse("tablecloth:javascript:alert(1)", out _));
        }

        [TestMethod]
        public void NonDeepLinkOrEmptyPayload_IsRejected()
        {
            Assert.IsFalse(TableClothUri.TryParse(null, out _));
            Assert.IsFalse(TableClothUri.TryParse(string.Empty, out _));
            Assert.IsFalse(TableClothUri.TryParse("https://www.wooribank.com/", out _));
            Assert.IsFalse(TableClothUri.TryParse("tablecloth:", out _));
            Assert.IsFalse(TableClothUri.TryParse("tablecloth://", out _));
        }

        [TestMethod]
        public void OverlongPayload_IsRejected()
        {
            var payload = "tablecloth:https://www.wooribank.com/?q=" + new string('a', TableClothUri.MaxLength);

            Assert.IsFalse(TableClothUri.TryParse(payload, out _));
        }

        [TestMethod]
        public void IsDeepLink_DetectsSchemeOnly()
        {
            Assert.IsTrue(TableClothUri.IsDeepLink("tablecloth:wooribank"));
            Assert.IsTrue(TableClothUri.IsDeepLink("  TABLECLOTH:whatever  "));
            Assert.IsFalse(TableClothUri.IsDeepLink("tableclothX:wooribank"));
            Assert.IsFalse(TableClothUri.IsDeepLink("https://www.wooribank.com/"));
            Assert.IsFalse(TableClothUri.IsDeepLink(null));
        }
    }
}
