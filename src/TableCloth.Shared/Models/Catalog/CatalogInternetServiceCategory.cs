using System;
using System.Xml.Serialization;

namespace TableCloth.Models.Catalog
{
    [Serializable, XmlType]
    public enum CatalogInternetServiceCategory : short
    {
        [XmlEnum(nameof(Other))]
        Other = 0,

        [XmlEnum(nameof(Banking))]
        Banking,

        [XmlEnum(nameof(Financing))]
        Financing,

        [XmlEnum(nameof(Security))]
        Security,

        [XmlEnum(nameof(Insurance))]
        Insurance,

        [XmlEnum(nameof(CreditCard))]
        CreditCard,

        [XmlEnum(nameof(Government))]
        Government,

        [XmlEnum(nameof(Education))]
        Education,
    }
}
