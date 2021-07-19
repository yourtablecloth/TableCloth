using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace TableCloth.Models
{
    [Serializable, XmlRoot("TableClothCatalog")]
    public class TableClothCatalog
    {
        [XmlArray, XmlArrayItem(typeof(InternetService), ElementName = "InternetServices")]
        public List<InternetService> InternetServices { get; set; } = new();
    }
}
