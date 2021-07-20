﻿using System;
using System.Xml.Serialization;

namespace TableCloth.Models.TableClothCatalog
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

        [XmlEnum("CreditCard")]
        CreditCard,

        [XmlEnum("Government")]
        Government,

        [XmlEnum("Education")]
        Education,
    }
}