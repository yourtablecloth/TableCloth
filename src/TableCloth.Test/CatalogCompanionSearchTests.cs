using TableCloth.Models.Catalog;

namespace TableCloth.Test
{
    /// <summary>
    /// 보조 프로그램 탭의 키워드 필터가 사용하는 <see cref="CatalogCompanion.IsMatchedItem(object, string)"/>
    /// 매칭 규칙을 검증한다. UI 없이 순수 매칭 로직만 확인한다.
    /// </summary>
    [TestClass]
    public sealed class CatalogCompanionSearchTests
    {
        private static CatalogCompanion CreateCompanion()
            => new CatalogCompanion
            {
                Id = "veraport",
                DisplayName = "Wizvera Veraport",
                Url = "https://example.com/veraport/setup.exe",
                Arguments = "/silent /norestart",
            };

        [TestMethod]
        public void EmptyFilter_MatchesEverything()
        {
            var item = CreateCompanion();

            Assert.IsTrue(CatalogCompanion.IsMatchedItem(item, string.Empty));
            Assert.IsTrue(CatalogCompanion.IsMatchedItem(item, "   "));
            Assert.IsTrue(CatalogCompanion.IsMatchedItem(item, null));
        }

        [TestMethod]
        public void Filter_ByDisplayName_IsCaseInsensitive()
        {
            var item = CreateCompanion();

            Assert.IsTrue(CatalogCompanion.IsMatchedItem(item, "wizvera"));
            Assert.IsTrue(CatalogCompanion.IsMatchedItem(item, "VERAPORT"));
        }

        [TestMethod]
        public void Filter_MatchesUrlIdAndArguments()
        {
            var item = CreateCompanion();

            Assert.IsTrue(CatalogCompanion.IsMatchedItem(item, "example.com")); // Url
            Assert.IsTrue(CatalogCompanion.IsMatchedItem(item, "veraport"));    // Id/DisplayName/Url 공통
            Assert.IsTrue(CatalogCompanion.IsMatchedItem(item, "silent"));      // Arguments
        }

        [TestMethod]
        public void Filter_NoMatch_ReturnsFalse()
        {
            var item = CreateCompanion();

            Assert.IsFalse(CatalogCompanion.IsMatchedItem(item, "no-such-keyword-xyz"));
        }

        [TestMethod]
        public void Filter_CommaSeparated_UsesOrMatching()
        {
            var item = CreateCompanion();

            // 한 토큰이라도 걸리면 표시(OR). 두 번째 토큰만 매칭돼도 true.
            Assert.IsTrue(CatalogCompanion.IsMatchedItem(item, "no-such-keyword, wizvera"));
            Assert.IsFalse(CatalogCompanion.IsMatchedItem(item, "nope-one, nope-two"));
        }

        [TestMethod]
        public void NonCompanionObject_ReturnsFalse()
        {
            Assert.IsFalse(CatalogCompanion.IsMatchedItem(new object(), "veraport"));
            Assert.IsFalse(CatalogCompanion.IsMatchedItem(null, "veraport"));
        }

        [TestMethod]
        public void NullFields_DoNotThrow_AndDoNotMatch()
        {
            // DisplayName 만 있고 나머지 필드가 null 인 항목도 예외 없이 안전하게 필터링돼야 한다.
            var item = new CatalogCompanion { DisplayName = "OnlyName" };

            Assert.IsTrue(CatalogCompanion.IsMatchedItem(item, "onlyname"));
            Assert.IsFalse(CatalogCompanion.IsMatchedItem(item, "veraport"));
        }
    }
}
