using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace TableCloth.Models.Catalog
{
    [Serializable, XmlRoot("TableClothCatalog")]
    public class CatalogDocument
    {
        [XmlArray(ElementName = "Companions"), XmlArrayItem(typeof(CatalogCompanion), ElementName = "Companion")]
        public List<CatalogCompanion> Companions { get; set; } = new List<CatalogCompanion>();

        [XmlArray(ElementName = "InternetServices"), XmlArrayItem(typeof(CatalogInternetService), ElementName = "Service")]
        public List<CatalogInternetService> Services { get; set; } = new List<CatalogInternetService>();
    }
}
