using System;
using System.Xml.Serialization;

namespace TableCloth.Models.Catalog
{
    [Serializable, XmlType]
    public enum CatalogInternetServiceCategory : short
    {
        [XmlEnum("Other")]
        Other = 0,

        [XmlEnum("Banking")]
        Banking,

        [XmlEnum("Financing")]
        Financing,

        [XmlEnum("Security")]
        Security,

        [XmlEnum("Insurance")]
        Insurance,

        [XmlEnum("CreditCard")]
        CreditCard,

        [XmlEnum("Government")]
        Government,

        [XmlEnum("Education")]
        Education,
    }
}
