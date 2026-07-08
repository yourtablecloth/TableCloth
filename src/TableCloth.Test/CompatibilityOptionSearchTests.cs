using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using TableCloth.ViewModels;

namespace TableCloth.Test
{
    /// <summary>
    /// 호환성 탭의 검색/필터 및 항목 어댑터(<see cref="CompatibilityOptionItem"/>)가
    /// 뷰모델 속성과 올바로 왕복하는지 검증한다. UI 없이 뷰모델 로직만 확인한다.
    /// </summary>
    [TestClass]
    public sealed class CompatibilityOptionSearchTests
    {
        // 프로덕션의 protected 생성자(설계 시점용)를 그대로 호출해 항목을 구성한다.
        // DI 서비스 없이도 BuildCompatibilityOptions가 동작하므로 테스트에서 안전하게 인스턴스화된다.
        private sealed class TestableOptionsWindowViewModel : OptionsWindowViewModel { }

        private static TestableOptionsWindowViewModel CreateViewModel() => new();

        [TestMethod]
        public void CompatibilityOptions_Initially_ExposesAllItems()
        {
            var vm = CreateViewModel();

            Assert.HasCount(3, vm.CompatibilityOptions);
            Assert.IsFalse(vm.HasNoCompatibilityMatches);
        }

        [TestMethod]
        public void Search_ByEnglishToken_FiltersToSingleItem()
        {
            var vm = CreateViewModel();

            vm.CompatibilityOptionSearchText = "ZScaler";

            Assert.HasCount(1, vm.CompatibilityOptions);
            Assert.IsFalse(vm.HasNoCompatibilityMatches);
        }

        [TestMethod]
        public void Search_ByKoreanToken_MatchesRegardlessOfCurrentUiLanguage()
        {
            var vm = CreateViewModel();

            // 검색 인덱스에 한국어와 영어를 함께 담으므로, 현재 UI 언어와 무관하게 한글로도 검색된다.
            vm.CompatibilityOptionSearchText = "인증서"; // ZScaler 항목의 한국어 텍스트

            Assert.HasCount(1, vm.CompatibilityOptions);
        }

        [TestMethod]
        public void Search_IsCaseInsensitive()
        {
            var vm = CreateViewModel();

            vm.CompatibilityOptionSearchText = "gpu";

            Assert.HasCount(1, vm.CompatibilityOptions);
        }

        [TestMethod]
        public void Search_NoMatch_SetsHasNoCompatibilityMatches()
        {
            var vm = CreateViewModel();

            vm.CompatibilityOptionSearchText = "no-such-setting-xyz";

            Assert.IsEmpty(vm.CompatibilityOptions);
            Assert.IsTrue(vm.HasNoCompatibilityMatches);
        }

        [TestMethod]
        public void Search_ClearingText_RestoresAllItems()
        {
            var vm = CreateViewModel();
            vm.CompatibilityOptionSearchText = "DNS";
            Assert.HasCount(1, vm.CompatibilityOptions);

            vm.CompatibilityOptionSearchText = string.Empty;

            Assert.HasCount(3, vm.CompatibilityOptions);
            Assert.IsFalse(vm.HasNoCompatibilityMatches);
        }

        [TestMethod]
        public void OptionItem_IsChecked_WritesThroughToOwnerProperty()
        {
            var vm = CreateViewModel();
            var gpuItem = vm.CompatibilityOptions.Single(x => x.SearchText.Contains("GPU"));

            Assert.IsFalse(vm.EnableSandboxGpuAcceleration);

            gpuItem.IsChecked = true;

            Assert.IsTrue(vm.EnableSandboxGpuAcceleration);
        }

        [TestMethod]
        public void OwnerProperty_Change_RaisesOptionItemIsCheckedNotification()
        {
            var vm = CreateViewModel();
            var dnsItem = vm.CompatibilityOptions.Single(x => x.SearchText.Contains("DNS"));

            // 이 알림이 있어야 초기 로드(OptionsWindowLoaded가 저장값을 뷰모델에 주입)가 이미 렌더된
            // 체크박스에 반영된다. CompatibilityOptionItem의 owner.PropertyChanged 구독이 그 통로다.
            var notified = new List<string?>();
            dnsItem.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

            vm.EnableSandboxPublicDnsFallback = false; // 기본 true → false

            Assert.IsFalse(dnsItem.IsChecked);
            CollectionAssert.Contains(notified, nameof(CompatibilityOptionItem.IsChecked));

            vm.EnableSandboxPublicDnsFallback = true;
            Assert.IsTrue(dnsItem.IsChecked);
        }
    }
}
