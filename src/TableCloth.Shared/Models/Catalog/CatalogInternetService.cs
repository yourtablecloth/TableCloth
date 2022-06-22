using System;
using System.Collections.Generic;
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
        /// <summary>
        /// 고유 아이디 값
        /// </summary>
        [XmlAttribute("Id")]
        public string Id { get; set; }

        /// <summary>
        /// 사용자에게 표시되는 이름
        /// </summary>
        [XmlAttribute("DisplayName")]
        public string DisplayName { get; set; }

        /// <summary>
        /// 서비스 분류
        /// </summary>
        [XmlAttribute("Category")]
        public CatalogInternetServiceCategory Category { get; set; }

        /// <summary>
        /// 서비스에 접속할 수 있는 URL
        /// </summary>
        [XmlAttribute("Url")]
        public string Url { get; set; }

        /// <summary>
        /// 사용자에게 고지해야 할 호환성 정보
        /// </summary>
        [XmlElement("CompatNotes")]
        public string CompatibilityNotes { get; set; }

        /// <summary>
        /// 서비스를 이용하기 위해 설치해야 하는 소프트웨어 정보 목록
        /// </summary>
        [XmlArray, XmlArrayItem(typeof(CatalogPackageInformation), ElementName = "Package")]
        public List<CatalogPackageInformation> Packages { get; set; } = new List<CatalogPackageInformation>();

        /// <summary>
        /// 서비스를 이용하기 위해 실행해야 하는 부트스트랩 스크립트
        /// </summary>
        /// <remarks>
        /// 이 속성은 <see cref="CustomBootstrapCDATA"/> 속성의 내부 저장 용도로 사용됩니다. 한 속성을 바꾸면 다른 속성도 내용이 변경됩니다.
        /// </remarks>
        [XmlIgnore]
        public string CustomBootstrap { get; set; }

        /// <summary>
        /// 서비스를 이용하기 위해 실행해야 하는 부트스트랩 스크립트
        /// </summary>
        /// <remarks>
        /// 이 속성은 <see cref="CustomBootstrap"/> 속성에 실제 데이터를 저장합니다. 한 속성을 바꾸면 다른 속성도 내용이 변경됩니다.
        /// </remarks>
        [XmlElement("CustomBootstrap")]
        public XmlCDataSection CustomBootstrapCDATA
        {
            get => new XmlDocument().CreateCDataSection(CustomBootstrap);
            set => CustomBootstrap = value.Value;
        }

        /// <summary>
        /// 서비스 분류 카테고리를 사용자에게 표시할 때 쓸 이름을 가져옵니다.
        /// </summary>
        /// <remarks>
        /// 이 속성은 실제 XML 데이터를 이용하여 값을 만드는 계산된 속성입니다.
        /// </remarks>
        [XmlIgnore]
        public string CategoryDisplayName
            => StringResources.InternetServiceCategory_DisplayText(Category);

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
                var actualPackageCount = Packages.Count;
                if (!string.IsNullOrWhiteSpace(CustomBootstrap))
                    actualPackageCount++;
                return actualPackageCount;
            }
        }

        /// <summary>
        /// 리스트 뷰에 표시될 아이콘의 상대적 크기 값을 가져옵니다.
        /// </summary>
        /// <remarks>
        /// 이 속성은 실제 XML 데이터를 이용하여 값을 만드는 계산된 속성입니다.
        /// </remarks>
        [XmlIgnore]
        public int ListViewIconSize => 16;

        /// <summary>
        /// 이 개체의 정보를 문자열로 가져옵니다.
        /// </summary>
        /// <returns>사용자가 이해할 수 있는 형태의 문자열이 반환됩니다.</returns>
        public override string ToString()
            => StringResources.InternetService_DisplayText(this);
    }
}
