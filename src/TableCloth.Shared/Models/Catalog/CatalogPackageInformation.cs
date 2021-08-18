using System;
using System.Xml.Serialization;

namespace TableCloth.Models.Catalog
{
    [Serializable, XmlType]
    public sealed class CatalogPackageInformation
    {
        [XmlAttribute("Name")]
        public string Name { get; set; }

        [XmlAttribute("Url")]
        public string Url { get; set; }

        [XmlAttribute("Arguments")]
        public string Arguments { get; set; }

        [XmlAttribute("SkipIEMode")]
        public bool SkipIEMode { get; set; }

        public override string ToString()
            => $"{Name} - {Url}";
    }
}
