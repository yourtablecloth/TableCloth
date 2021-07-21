using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace TableCloth.Models.Catalog
{
    [Serializable, XmlRoot("TableClothCatalog")]
    public class CatalogDocument
    {
        [XmlArray(ElementName = "InternetServices"), XmlArrayItem(typeof(CatalogInternetService), ElementName = "Service")]
        public List<CatalogInternetService> Services { get; set; } = new();
    }
}
