using TableCloth.Models.Catalog;

namespace TableCloth.Test
{
    /// <summary>
    /// 무설치 `.wsb` 딥링크 / 브라우저 익스텐션이 넘기는 대상 URL 게이트
    /// (<see cref="CatalogTargetUrlMatcher"/>)를 검증한다.
    /// </summary>
    /// <remarks>
    /// 픽스처의 호스트 구성은 실제 카탈로그에서 가져온 것이며, 각각이 구현에서 실수하기 쉬운
    /// 지점을 대표한다.
    /// <list type="bullet">
    /// <item>우리은행 개인/기업이 <c>wooribank.com</c> 을 공유 → 도메인만으로는 패키지 확정 불가.</item>
    /// <item><c>ibk.co.kr</c> → "끝 두 라벨" 계산이면 <c>co.kr</c> 이 되어 모든 .co.kr 이 서로 일치.</item>
    /// <item><c>fsb.or.kr</c> 아래 저축은행 다수 → 유일 승자 판정과 동점 판정이 모두 필요.</item>
    /// <item>제주은행은 URL 에 포트가 붙어 있음.</item>
    /// </list>
    /// </remarks>
    [TestClass]
    public sealed class CatalogTargetUrlMatcherTests
    {
        private static CatalogDocument CreateCatalog()
            => new CatalogDocument
            {
                Services =
                [
                    new CatalogInternetService { Id = "WooriBank", DisplayName = "우리은행 개인뱅킹", Url = "https://www.wooribank.com/" },
                    new CatalogInternetService { Id = "WooriBankBiz", DisplayName = "우리은행 기업뱅킹", Url = "https://nbi.wooribank.com/" },
                    new CatalogInternetService { Id = "IBKBank", DisplayName = "IBK기업은행 개인뱅킹", Url = "https://www.ibk.co.kr/" },
                    // fsb.or.kr 은 서로 다른 저축은행들이 호스팅만 공유하는 도메인이다(실제 카탈로그엔 25곳).
                    // 도메인당 후보 수가 경계를 넘도록 실제와 비슷하게 여러 개를 둔다.
                    new CatalogInternetService { Id = "OKSavingsBank", DisplayName = "OK저축은행", Url = "https://ok.ibs.fsb.or.kr/" },
                    new CatalogInternetService { Id = "JTSavingsBank", DisplayName = "JT저축은행", Url = "https://jt.ibs.fsb.or.kr/" },
                    new CatalogInternetService { Id = "HBSavingsBank", DisplayName = "HB저축은행", Url = "https://hbsb.ibs.fsb.or.kr/" },
                    new CatalogInternetService { Id = "KukjeSavingsBank", DisplayName = "국제저축은행", Url = "https://kukje.ibs.fsb.or.kr/" },
                    new CatalogInternetService { Id = "JejuBank", DisplayName = "제주은행", Url = "https://bank.jejubank.co.kr:6443/" },
                ],
            };

        // 이슈 제기 시 예시로 쓰인 실제 주소. 쿼리스트링에 '&' 가 들어 있어 XML 이스케이프 계약도 함께 대표한다.
        private const string WooriDeepLinkUrl =
            "https://spib.wooribank.com/pib/Dream?withyou=CTCER0149&fromSite=pib";

        [TestMethod]
        public void SiteIdSuppliedByProducer_SubdomainOfSameRegistrableDomain_IsAccepted()
        {
            var result = CatalogTargetUrlMatcher.Match(CreateCatalog(), WooriDeepLinkUrl, ["WooriBank"]);

            Assert.IsTrue(result.IsAccepted);
            Assert.AreEqual(CatalogTargetUrlRejectionReason.None, result.Reason);
            CollectionAssert.AreEqual(new[] { "WooriBank" }, result.ServiceIds.ToArray());

            // 재정규화 없이 원문 그대로 열어야 한다(퍼센트 인코딩된 파라미터 보존).
            Assert.AreEqual(WooriDeepLinkUrl, result.AcceptedUrl);
        }

        [TestMethod]
        public void MultipleSiteIds_AllWithinSameDomain_AreAccepted()
        {
            // 익스텐션이 개인/기업을 구분하지 못해 둘 다 넘긴 경우. 같은 은행이므로 수락한다.
            var result = CatalogTargetUrlMatcher.Match(
                CreateCatalog(), WooriDeepLinkUrl, ["WooriBank", "WooriBankBiz"]);

            Assert.IsTrue(result.IsAccepted);
            CollectionAssert.AreEqual(new[] { "WooriBank", "WooriBankBiz" }, result.ServiceIds.ToArray());
        }

        [TestMethod]
        public void SiteIdOutsideTargetDomain_IsRejected_SoUnrelatedPackagesCannotPiggyback()
        {
            // URL 은 우리은행인데 무관한 저축은행 Id 가 끼어든 `.wsb`. 제3자 보안 프로그램 설치 편승을 막는다.
            var result = CatalogTargetUrlMatcher.Match(
                CreateCatalog(), WooriDeepLinkUrl, ["WooriBank", "OKSavingsBank"]);

            Assert.IsFalse(result.IsAccepted);
            Assert.AreEqual(CatalogTargetUrlRejectionReason.NoCatalogDomainMatch, result.Reason);
        }

        [TestMethod]
        public void UnrelatedUrl_IsRejected_EvenWhenSiteIdIsValid()
        {
            var result = CatalogTargetUrlMatcher.Match(CreateCatalog(), "https://www.naver.com/", ["WooriBank"]);

            Assert.IsFalse(result.IsAccepted);
            Assert.AreEqual(CatalogTargetUrlRejectionReason.NoCatalogDomainMatch, result.Reason);
        }

        [TestMethod]
        public void UnrelatedUrl_WithoutSiteIds_IsRejected()
        {
            var result = CatalogTargetUrlMatcher.Match(CreateCatalog(), "https://www.naver.com/");

            Assert.IsFalse(result.IsAccepted);
            Assert.AreEqual(CatalogTargetUrlRejectionReason.NoCatalogDomainMatch, result.Reason);
        }

        [TestMethod]
        public void SecondLevelKoreanSuffix_DoesNotMakeEveryCoKrSiteMatch()
        {
            // co.kr 을 퍼블릭 서픽스로 인식하지 못하면 이 주소가 IBKBank 와 일치해버린다.
            var result = CatalogTargetUrlMatcher.Match(CreateCatalog(), "https://www.example.co.kr/login");

            Assert.IsFalse(result.IsAccepted);
            Assert.AreEqual(CatalogTargetUrlRejectionReason.NoCatalogDomainMatch, result.Reason);
        }

        [TestMethod]
        public void LookalikeHostWithoutLabelBoundary_IsRejected()
        {
            // 문자열 EndsWith 로 판정하면 통과하는 형태.
            var result = CatalogTargetUrlMatcher.Match(CreateCatalog(), "https://www.evilwooribank.com/");

            Assert.IsFalse(result.IsAccepted);
            Assert.AreEqual(CatalogTargetUrlRejectionReason.NoCatalogDomainMatch, result.Reason);
        }

        [TestMethod]
        public void CatalogDomainUsedAsPrefixOfAnotherDomain_IsRejected()
        {
            // 문자열 Contains 로 판정하면 통과하는 형태.
            var result = CatalogTargetUrlMatcher.Match(CreateCatalog(), "https://wooribank.com.evil.kr/pib");

            Assert.IsFalse(result.IsAccepted);
            Assert.AreEqual(CatalogTargetUrlRejectionReason.NoCatalogDomainMatch, result.Reason);
        }

        [TestMethod]
        public void SharedHostingDomain_ExactHostWins_WithoutSiteIds()
        {
            // fsb.or.kr 아래 여러 저축은행이 있어도 호스트 라벨이 더 많이 일치하는 쪽이 유일 승자가 된다.
            var result = CatalogTargetUrlMatcher.Match(
                CreateCatalog(), "https://ok.ibs.fsb.or.kr/ib20/mnu/FPMDPT010000000");

            Assert.IsTrue(result.IsAccepted);
            CollectionAssert.AreEqual(new[] { "OKSavingsBank" }, result.ServiceIds.ToArray());
        }

        [TestMethod]
        public void SharedHostingDomain_UnknownHost_IsAmbiguous_WithoutSiteIds()
        {
            var result = CatalogTargetUrlMatcher.Match(CreateCatalog(), "https://unknown.ibs.fsb.or.kr/");

            Assert.IsFalse(result.IsAccepted);
            Assert.AreEqual(CatalogTargetUrlRejectionReason.AmbiguousCandidates, result.Reason);
        }

        [TestMethod]
        public void SameCompanyDomain_TiedCandidates_ResolveToASingleSiteInCatalogOrder()
        {
            // spib.wooribank.com 은 개인(www)/기업(nbi) 어느 쪽과도 라벨이 더 일치하지 않아 동점이다.
            // 사이트 Id 판정은 항상 하나로 끝나야 한다 — 여러 개를 함께 설치하면 겹치는 패키지(AnySign,
            // AhnLabSafeTx, nProtect, IPInside)가 중복 설치되어 단계 목록이 지저분해진다(실측 제보).
            var result = CatalogTargetUrlMatcher.Match(CreateCatalog(), WooriDeepLinkUrl);

            Assert.IsTrue(result.IsAccepted);
            CollectionAssert.AreEqual(new[] { "WooriBank" }, result.ServiceIds.ToArray(),
                "동점이면 카탈로그에 먼저 적힌 항목(개인뱅킹)으로 확정되어야 합니다.");
            Assert.AreEqual(WooriDeepLinkUrl, result.AcceptedUrl,
                "URL 형식의 차이는 '열 주소'뿐이어야 합니다.");
        }

        [TestMethod]
        public void SharedHostingDomain_UnknownHost_IsRejected_NotGuessed()
        {
            // fsb.or.kr 아래는 서로 '다른 회사'라, 그중 하나를 골라 남의 은행 보안 프로그램을 깔면 안 된다.
            // 우리은행 개인/기업 같은 '같은 회사의 갈래'와 구분되는 지점이다.
            var result = CatalogTargetUrlMatcher.Match(CreateCatalog(), "https://unknown.ibs.fsb.or.kr/");

            Assert.IsFalse(result.IsAccepted);
            Assert.AreEqual(CatalogTargetUrlRejectionReason.AmbiguousCandidates, result.Reason);
        }

        [TestMethod]
        public void HostWithExplicitPort_IsHandled()
        {
            var result = CatalogTargetUrlMatcher.Match(
                CreateCatalog(), "https://bank.jejubank.co.kr:6443/jeju/index.jsp", ["JejuBank"]);

            Assert.IsTrue(result.IsAccepted);
        }

        [TestMethod]
        public void EmbeddedCredentials_AreRejected()
        {
            // Uri 는 호스트를 evil.kr 로 올바르게 파싱하지만, 사용자에게 URL 을 보여줄 때 혼동을 준다.
            var result = CatalogTargetUrlMatcher.Match(
                CreateCatalog(), "https://www.wooribank.com@evil.kr/pib", ["WooriBank"]);

            Assert.IsFalse(result.IsAccepted);
            Assert.AreEqual(CatalogTargetUrlRejectionReason.EmbeddedCredentials, result.Reason);
        }

        [TestMethod]
        public void NonHttpSchemes_AreRejected()
        {
            var catalog = CreateCatalog();

            Assert.AreEqual(
                CatalogTargetUrlRejectionReason.UnsupportedScheme,
                CatalogTargetUrlMatcher.Match(catalog, "file:///C:/Windows/System32/calc.exe").Reason);

            Assert.AreEqual(
                CatalogTargetUrlRejectionReason.UnsupportedScheme,
                CatalogTargetUrlMatcher.Match(catalog, "ftp://www.wooribank.com/pub").Reason);
        }

        [TestMethod]
        public void WhitespaceInUrl_IsRejected_SoBrowserSwitchesCannotBeAppended()
        {
            var result = CatalogTargetUrlMatcher.Match(
                CreateCatalog(),
                "https://www.wooribank.com/ --load-extension=C:\\Temp\\evil",
                ["WooriBank"]);

            Assert.IsFalse(result.IsAccepted);
            Assert.AreEqual(CatalogTargetUrlRejectionReason.UnsafeCharacters, result.Reason);
        }

        [TestMethod]
        public void UnsubstitutedPlaceholderOrEmptyValue_CountsAsNotSpecified()
        {
            var catalog = CreateCatalog();

            Assert.AreEqual(
                CatalogTargetUrlRejectionReason.NotSpecified,
                CatalogTargetUrlMatcher.Match(catalog, "__SPORK_TARGET_URL__").Reason);

            Assert.AreEqual(
                CatalogTargetUrlRejectionReason.NotSpecified,
                CatalogTargetUrlMatcher.Match(catalog, "   ").Reason);

            Assert.AreEqual(
                CatalogTargetUrlRejectionReason.NotSpecified,
                CatalogTargetUrlMatcher.Match(catalog, null).Reason);
        }

        [TestMethod]
        public void OverlongUrl_IsRejected()
        {
            var longUrl = "https://www.wooribank.com/pib?q=" + new string('a', CatalogTargetUrlMatcher.MaxTargetUrlLength);

            Assert.AreEqual(
                CatalogTargetUrlRejectionReason.TooLong,
                CatalogTargetUrlMatcher.Match(CreateCatalog(), longUrl, ["WooriBank"]).Reason);
        }

        [TestMethod]
        public void UnknownSiteIds_FallBackToUrlOnlyResolution()
        {
            // 카탈로그에 없는 Id 만 온 경우(오래된 `.wsb`) URL 자체로 판정한다.
            var result = CatalogTargetUrlMatcher.Match(
                CreateCatalog(), "https://ok.ibs.fsb.or.kr/", ["NoSuchSiteId"]);

            Assert.IsTrue(result.IsAccepted);
            CollectionAssert.AreEqual(new[] { "OKSavingsBank" }, result.ServiceIds.ToArray());
        }

        [TestMethod]
        public void TryGetRegistrableDomain_RespectsPublicSuffixes()
        {
            Assert.IsTrue(CatalogTargetUrlMatcher.TryGetRegistrableDomain("spib.wooribank.com", out var woori));
            Assert.AreEqual("wooribank.com", woori);

            Assert.IsTrue(CatalogTargetUrlMatcher.TryGetRegistrableDomain("WWW.IBK.CO.KR", out var ibk));
            Assert.AreEqual("ibk.co.kr", ibk);

            Assert.IsTrue(CatalogTargetUrlMatcher.TryGetRegistrableDomain("ok.ibs.fsb.or.kr", out var fsb));
            Assert.AreEqual("fsb.or.kr", fsb);

            // 호스트가 곧 퍼블릭 서픽스이면 등록 도메인이 없다.
            Assert.IsFalse(CatalogTargetUrlMatcher.TryGetRegistrableDomain("co.kr", out _));
            Assert.IsFalse(CatalogTargetUrlMatcher.TryGetRegistrableDomain("com", out _));

            // 서픽스 목록이 불완전한 경우의 과다 일치 가드(일반 토큰은 등록 도메인 첫 라벨이 될 수 없음).
            Assert.IsFalse(CatalogTargetUrlMatcher.TryGetRegistrableDomain("www.co.za", out _));
        }
    }
}
