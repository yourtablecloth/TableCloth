using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace TableCloth.Models.TableClothCatalog
{
    [Serializable, XmlRoot("TableClothCatalog")]
    public class CatalogDocument
    {
        [XmlArray, XmlArrayItem(typeof(CatalogInternetService), ElementName = "InternetServices")]
        public List<CatalogInternetService> InternetServices { get; set; } = new();
    }
}
