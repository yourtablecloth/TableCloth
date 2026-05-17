using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using TableCloth.Resources;

namespace TableCloth.Models.Catalog
{
    /// <summary>
    /// 특정 서비스 및 해당 서비스용 소프트웨어에 대한 정보를 담는 XML 요소를 나타냅니다.
    /// </summary>
    public sealed class CatalogInternetService : System.ComponentModel.INotifyPropertyChanged
    {
        private static readonly char[] SearchKeywordsSeparators = new char[] { ';', };

        private static readonly char[] FilterTextSeparators = new char[] { ',', };

        /// <summary>
        /// 고유 아이디 값
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 사용자에게 표시되는 이름 - 기본
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 서비스 분류
        /// </summary>
        public CatalogInternetServiceCategory Category { get; set; } = CatalogInternetServiceCategory.Other;

        /// <summary>
        /// 서비스에 접속할 수 있는 URL
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// 사용자에게 고지해야 할 호환성 정보
        /// </summary>
        public string CompatibilityNotes { get; set; } = null;

        public bool IsFavorite { get; set; }

        /// <summary>
        /// 카탈로그가 정의한 모든 패키지/Edge 확장/CustomBootstrap 스크립트가 영속 저장소에 fingerprint
        /// 로 기록되어 있어 다음 진입 시 StepsComposer 가 모두 건너뛸 상태인지 여부.
        /// 카탈로그 진입 직후와 설치 완료 후 catalog 뷰로 돌아올 때 MainWindowViewModel 이 재계산해
        /// UI 배지의 표시 여부를 결정한다.
        /// </summary>
        /// <remarks>
        /// 단일 INotifyPropertyChanged 트리거가 필요한 유일한 멤버이므로 ObservableObject 전체 도입 대신
        /// 수동 PropertyChanged 발화. WPF 바인딩은 이 이벤트로 자동 갱신된다.
        /// </remarks>
        public bool IsAllInstalled
        {
            get => _isAllInstalled;
            set
            {
                if (_isAllInstalled == value)
                    return;
                _isAllInstalled = value;
                PropertyChanged?.Invoke(this, IsAllInstalledChangedArgs);
            }
        }
        private bool _isAllInstalled;
        private static readonly System.ComponentModel.PropertyChangedEventArgs IsAllInstalledChangedArgs
            = new System.ComponentModel.PropertyChangedEventArgs(nameof(IsAllInstalled));

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 서비스를 이용하기 위해 설치해야 하는 소프트웨어 정보 목록
        /// </summary>
        public List<CatalogPackageInformation> Packages { get; set; } = new List<CatalogPackageInformation>();

        public List<CatalogEdgeExtensionInformation> EdgeExtensions { get; set; } = new List<CatalogEdgeExtensionInformation>();

        /// <summary>
        /// 서비스를 이용하기 위해 실행해야 하는 부트스트랩 스크립트
        /// </summary>
        /// <remarks>
        /// 이 속성은 <see cref="CustomBootstrapCDATA"/> 속성의 내부 저장 용도로 사용됩니다. 한 속성을 바꾸면 다른 속성도 내용이 변경됩니다.
        /// </remarks>
        public string CustomBootstrap { get; set; } = null;

        /// <summary>
        /// 서비스를 이용하기 위해 실행해야 하는 부트스트랩 스크립트
        /// </summary>
        /// <remarks>
        /// 이 속성은 <see cref="CustomBootstrap"/> 속성에 실제 데이터를 저장합니다. 한 속성을 바꾸면 다른 속성도 내용이 변경됩니다.
        /// </remarks>
        public XmlCDataSection CustomBootstrapCDATA
        {
            get => new XmlDocument().CreateCDataSection(CustomBootstrap);
            set => CustomBootstrap = value?.Value;
        }

        public string SearchKeywords { get; set; } = null;

        /// <summary>
        /// 서비스 분류 카테고리를 사용자에게 표시할 때 쓸 이름을 가져옵니다.
        /// </summary>
        /// <remarks>
        /// 이 속성은 실제 XML 데이터를 이용하여 값을 만드는 계산된 속성입니다.
        /// </remarks>
        public string CategoryDisplayName
        {
            get
            {
                switch (Category)
                {
                    case CatalogInternetServiceCategory.Banking: return CommonStrings.DisplayName_Banking;
                    case CatalogInternetServiceCategory.CreditCard: return CommonStrings.DisplayName_CreditCard;
                    case CatalogInternetServiceCategory.Education: return CommonStrings.DisplayName_Education;
                    case CatalogInternetServiceCategory.Financing: return CommonStrings.DisplayName_Financing;
                    case CatalogInternetServiceCategory.Government: return CommonStrings.DisplayName_Government;
                    case CatalogInternetServiceCategory.Security: return CommonStrings.DisplayName_Security;
                    case CatalogInternetServiceCategory.Insurance: return CommonStrings.DisplayName_Insurance;
                    case CatalogInternetServiceCategory.Other:
                    default: return CommonStrings.DisplayName_Other;
                }
            }
        }

        /// <summary>
        /// 설치해야 하는 소프트웨어의 숫자를 가져옵니다.
        /// </summary>
        /// <remarks>
        /// 이 속성은 실제 XML 데이터를 이용하여 값을 만드는 계산된 속성입니다.
        /// </remarks>
        public int PackageCountForDisplay
        {
            get
            {
                var actualPackageCount = Packages.Count + EdgeExtensions.Count;
                if (!string.IsNullOrWhiteSpace(CustomBootstrap))
                    actualPackageCount++;
                return actualPackageCount;
            }
        }

        /// <summary>
        /// 이 개체의 정보를 문자열로 가져옵니다.
        /// </summary>
        /// <returns>사용자가 이해할 수 있는 형태의 문자열이 반환됩니다.</returns>
        public override string ToString()
        {
            var defaultString = $"{DisplayName} - {Url}";
            var pkgs = Packages;

            var hasCompatNotes = !string.IsNullOrWhiteSpace(CompatibilityNotes);

            if (hasCompatNotes)
                defaultString = $"*{defaultString}";

            if (pkgs != null && pkgs.Count > 0)
                defaultString = $"{defaultString} (Total {PackageCountForDisplay} package(s) required.)";

            return defaultString;
        }

        public IEnumerable<string> GetSearchKeywords()
            => (SearchKeywords ?? string.Empty).Split(SearchKeywordsSeparators, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Distinct().ToArray();

        public static bool IsMatchedItem(object item, string filterText)
        {
            var actualItem = item as CatalogInternetService;

            if (actualItem == null)
                return false;

            if (string.IsNullOrWhiteSpace(filterText))
                return true;

            var result = false;
            var splittedFilterText = filterText.Split(FilterTextSeparators, StringSplitOptions.RemoveEmptyEntries);

            foreach (var eachFilterText in splittedFilterText)
            {
                result |= actualItem.DisplayName.IndexOf(eachFilterText, StringComparison.OrdinalIgnoreCase) > (-1)
                    || actualItem.CategoryDisplayName.IndexOf(eachFilterText, StringComparison.OrdinalIgnoreCase) > (-1)
                    || actualItem.Url.IndexOf(eachFilterText, StringComparison.OrdinalIgnoreCase) > (-1)
                    || actualItem.Packages.Count.ToString().IndexOf(eachFilterText, StringComparison.OrdinalIgnoreCase) > (-1)
                    || actualItem.Packages.Any(x => x.Name.IndexOf(eachFilterText, StringComparison.OrdinalIgnoreCase) > (-1))
                    || actualItem.Id.IndexOf(eachFilterText, StringComparison.OrdinalIgnoreCase) > (-1)
                    || actualItem.GetSearchKeywords().Any(x => x.IndexOf(eachFilterText, StringComparison.OrdinalIgnoreCase) > (-1));
            }

            return result;
        }

        public static bool IsMatchedItem(object item, string filterText, bool isFavoriteOnly)
        {
            var actualItem = item as CatalogInternetService;

            if (actualItem == null)
                return false;

            if (isFavoriteOnly && !actualItem.IsFavorite)
                return false;

            if (string.IsNullOrWhiteSpace(filterText))
                return true;

            var result = false;
            var splittedFilterText = filterText.Split(FilterTextSeparators, StringSplitOptions.RemoveEmptyEntries);

            foreach (var eachFilterText in splittedFilterText)
            {
                result |= actualItem.DisplayName.IndexOf(eachFilterText, StringComparison.OrdinalIgnoreCase) > (-1)
                          || actualItem.CategoryDisplayName.IndexOf(eachFilterText, StringComparison.OrdinalIgnoreCase) > (-1)
                          || actualItem.Url.IndexOf(eachFilterText, StringComparison.OrdinalIgnoreCase) > (-1)
                          || actualItem.Packages.Count.ToString().IndexOf(eachFilterText, StringComparison.OrdinalIgnoreCase) > (-1)
                          || actualItem.Packages.Any(x => x.Name.IndexOf(eachFilterText, StringComparison.OrdinalIgnoreCase) > (-1))
                          || actualItem.Id.IndexOf(eachFilterText, StringComparison.OrdinalIgnoreCase) > (-1)
                          || actualItem.GetSearchKeywords().Any(x => x.IndexOf(eachFilterText, StringComparison.OrdinalIgnoreCase) > (-1));
            }

            return result;
        }
    }
}
