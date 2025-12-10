using System;
using System.Xml.Serialization;

namespace TableCloth.Models.Catalog
{
    /// <summary>
    /// 서비스에 관계없이 공용으로 사용할 수 있는 소프트웨어에 대한 정보를 담는 XML 요소를 나타냅니다.
    /// </summary>
    [Serializable, XmlType]
    public sealed class CatalogCompanion
    {
        /// <summary>
        /// 고유 아이디 값
        /// </summary>
        [XmlAttribute("Id")]
        public string Id { get; set; } = null;

        /// <summary>
        /// 사용자에게 표시될 이름
        /// </summary>
        [XmlAttribute("DisplayName")]
        public string DisplayName { get; set; } = null;

        /// <summary>
        /// 소프트웨어를 다운로드할 수 있는 URL
        /// </summary>
        [XmlAttribute("Url")]
        public string Url { get; set; } = null;

        /// <summary>
        /// 설치 프로그램 실행 시 전달할 매개 변수
        /// </summary>
        [XmlAttribute("Arguments")]
        public string Arguments { get; set; } = null;
    }
}
