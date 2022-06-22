using System;
using System.Xml.Serialization;

namespace TableCloth.Models.Catalog
{
    /// <summary>
    /// 설치해야 하는 소프트웨어의 정보를 담는 XML 요소를 나타냅니다.
    /// </summary>
    [Serializable, XmlType]
    public sealed class CatalogPackageInformation
    {
        /// <summary>
        /// 소프트웨어 패키지의 기술적 명칭 (영어와 숫자로 된 이름을 권장)
        /// </summary>
        [XmlAttribute("Name")]
        public string Name { get; set; }

        /// <summary>
        /// 설치 프로그램을 다운로드할 수 있는 URL
        /// </summary>
        [XmlAttribute("Url")]
        public string Url { get; set; }

        /// <summary>
        /// 설치 프로그램 실행 시 전달해야 하는 매개 변수
        /// </summary>
        [XmlAttribute("Arguments")]
        public string Arguments { get; set; }

        /// <summary>
        /// 이 개체의 정보를 문자열로 가져옵니다.
        /// </summary>
        /// <returns>사용자가 이해할 수 있는 형태의 문자열이 반환됩니다.</returns>
        public override string ToString()
            => $"{Name} - {Url}";
    }
}
