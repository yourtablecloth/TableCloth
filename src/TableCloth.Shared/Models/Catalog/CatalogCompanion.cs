using System;
using System.Xml.Serialization;

namespace TableCloth.Models.Catalog
{
    [Serializable, XmlType]
    public sealed class CatalogCompanion
    {
        [XmlAttribute("Id")]
        public string Id { get; set; }

        [XmlAttribute("DisplayName")]
        public string DisplayName { get; set; }

        [XmlAttribute("Url")]
        public string Url { get; set; }

        [XmlAttribute("Arguments")]
        public string Arguments { get; set; }
    }
}
