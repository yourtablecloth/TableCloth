using System;
using System.Xml.Serialization;

namespace TableCloth.Models.Catalog
{
    /// <summary>
    /// 카탈로그 상의 인터넷 서비스 분류를 나타냅니다.
    /// </summary>
    [Serializable, XmlType]
    public enum CatalogInternetServiceCategory : short
    {
        /// <summary>
        /// 기타
        /// </summary>
        [XmlEnum(nameof(Other))]
        Other = 0,

        /// <summary>
        /// 인터넷 뱅킹
        /// </summary>
        [XmlEnum(nameof(Banking))]
        Banking,

        /// <summary>
        /// 금융
        /// </summary>
        [XmlEnum(nameof(Financing))]
        Financing,

        /// <summary>
        /// 투자
        /// </summary>
        [XmlEnum(nameof(Security))]
        Security,

        /// <summary>
        /// 보험
        /// </summary>
        [XmlEnum(nameof(Insurance))]
        Insurance,

        /// <summary>
        /// 신용 카드
        /// </summary>
        [XmlEnum(nameof(CreditCard))]
        CreditCard,

        /// <summary>
        /// 정부, 공공기관
        /// </summary>
        [XmlEnum(nameof(Government))]
        Government,

        /// <summary>
        /// 교육
        /// </summary>
        [XmlEnum(nameof(Education))]
        Education,
    }
}
