using System;
using System.Xml.Serialization;

namespace TableCloth.Models
{
    [Serializable, XmlType]
    public sealed class PackageInformation
    {
        [XmlAttribute("Name")]
        public string Name { get; set; }

        [XmlAttribute("Url")]
        public Uri Url { get; set; }

        [XmlAttribute("Arguments")]
        public string Arguments { get; set; }

        public override string ToString()
            => $"{Name} - {Url}";
    }
}
