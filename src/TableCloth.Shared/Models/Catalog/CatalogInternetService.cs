using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using TableCloth.Resources;

namespace TableCloth.Models.Catalog
{
    /// <summary>
    /// 특정 서비스 및 해당 서비스용 소프트웨어에 대한 정보를 담는 XML 요소를 나타냅니다.
    /// </summary>
    [Serializable, XmlType]
    public sealed class CatalogInternetService
    {
        [XmlIgnore]
        private static readonly char[] SearchKeywordsSeparators = new char[] { ';', };

        [XmlIgnore]
        private static readonly char[] FilterTextSeparators = new char[] { ',', };

        /// <summary>
        /// 고유 아이디 값
        /// </summary>
        [XmlAttribute("Id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 사용자에게 표시되는 이름 - 기본
        /// </summary>
        [XmlAttribute("DisplayName")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 서비스 분류
        /// </summary>
        [XmlAttribute("Category")]
        public CatalogInternetServiceCategory Category { get; set; } = CatalogInternetServiceCategory.Other;

        /// <summary>
        /// 서비스에 접속할 수 있는 URL
        /// </summary>
        [XmlAttribute("Url")]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// 사용자에게 고지해야 할 호환성 정보
        /// </summary>
        [XmlElement("CompatNotes")]
        public string
#if !NETFX
            ?
#endif
            CompatibilityNotes
        { get; set; }

        /// <summary>
        /// 서비스를 이용하기 위해 설치해야 하는 소프트웨어 정보 목록
        /// </summary>
        [XmlArray, XmlArrayItem(typeof(CatalogPackageInformation), ElementName = "Package")]
#pragma warning disable IDE0028 // Simplify collection initialization
        public List<CatalogPackageInformation> Packages { get; set; } = new List<CatalogPackageInformation>();
#pragma warning restore IDE0028 // Simplify collection initialization

        [XmlArray, XmlArrayItem(typeof(CatalogEdgeExtensionInformation), ElementName = "EdgeExtension")]
        public List<CatalogEdgeExtensionInformation> EdgeExtensions { get; set; } = new List<CatalogEdgeExtensionInformation>();

        /// <summary>
        /// 서비스를 이용하기 위해 실행해야 하는 부트스트랩 스크립트
        /// </summary>
        /// <remarks>
        /// 이 속성은 <see cref="CustomBootstrapCDATA"/> 속성의 내부 저장 용도로 사용됩니다. 한 속성을 바꾸면 다른 속성도 내용이 변경됩니다.
        /// </remarks>
        [XmlIgnore]
        public string
#if !NETFX
            ?
#endif
            CustomBootstrap
        { get; set; }

        /// <summary>
        /// 서비스를 이용하기 위해 실행해야 하는 부트스트랩 스크립트
        /// </summary>
        /// <remarks>
        /// 이 속성은 <see cref="CustomBootstrap"/> 속성에 실제 데이터를 저장합니다. 한 속성을 바꾸면 다른 속성도 내용이 변경됩니다.
        /// </remarks>
        [XmlElement("CustomBootstrap")]
        public XmlCDataSection
#if !NETFX
            ?
#endif
            CustomBootstrapCDATA
        {
            get => new XmlDocument().CreateCDataSection(CustomBootstrap);
            set => CustomBootstrap = value?.Value;
        }

        [XmlElement("SearchKeywords")]
        public string
#if !NETFX
            ?
#endif
            SearchKeywords
        { get; set; }

        /// <summary>
        /// 서비스 분류 카테고리를 사용자에게 표시할 때 쓸 이름을 가져옵니다.
        /// </summary>
        /// <remarks>
        /// 이 속성은 실제 XML 데이터를 이용하여 값을 만드는 계산된 속성입니다.
        /// </remarks>
        [XmlIgnore]
        public string CategoryDisplayName
        {
            get
            {
#pragma warning disable IDE0066 // Convert switch statement to expression
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
#pragma warning restore IDE0066 // Convert switch statement to expression
            }
        }

        /// <summary>
        /// 설치해야 하는 소프트웨어의 숫자를 가져옵니다.
        /// </summary>
        /// <remarks>
        /// 이 속성은 실제 XML 데이터를 이용하여 값을 만드는 계산된 속성입니다.
        /// </remarks>
        [XmlIgnore]
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

    }
}
