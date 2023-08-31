using System;
using System.Xml.Serialization;
using TableCloth.Resources;

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
        [EnumDisplayOrder(7)]
        [EnumDisplayName(StringResources.DisplayName_Other)]
        Other = 0,

        /// <summary>
        /// 인터넷 뱅킹
        /// </summary>
        [XmlEnum(nameof(Banking))]
        [EnumDisplayOrder(1)]
        [EnumDisplayName(StringResources.DisplayName_Banking)]
        Banking,

        /// <summary>
        /// 금융
        /// </summary>
        [XmlEnum(nameof(Financing))]
        [EnumDisplayOrder(2)]
        [EnumDisplayName(StringResources.DisplayName_Financing)]
        Financing,

        /// <summary>
        /// 투자
        /// </summary>
        [XmlEnum(nameof(Security))]
        [EnumDisplayOrder(3)]
        [EnumDisplayName(StringResources.DisplayName_Security)]
        Security,

        /// <summary>
        /// 보험
        /// </summary>
        [XmlEnum(nameof(Insurance))]
        [EnumDisplayOrder(4)]
        [EnumDisplayName(StringResources.DisplayName_Insurance)]
        Insurance,

        /// <summary>
        /// 신용 카드
        /// </summary>
        [XmlEnum(nameof(CreditCard))]
        [EnumDisplayOrder(5)]
        [EnumDisplayName(StringResources.DisplayName_CreditCard)]
        CreditCard,

        /// <summary>
        /// 정부, 공공기관
        /// </summary>
        [XmlEnum(nameof(Government))]
        [EnumDisplayOrder(6)]
        [EnumDisplayName(StringResources.DisplayName_Government)]
        Government,

        /// <summary>
        /// 교육
        /// </summary>
        [XmlEnum(nameof(Education))]
        [EnumDisplayOrder(8)]
        [EnumDisplayName(StringResources.DisplayName_Education)]
        Education,
    }
}
