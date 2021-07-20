using System;
using System.Xml;
using System.Xml.Serialization;

namespace TableCloth.Models.WindowsSandbox
{
    [Serializable, XmlType("MappedFolder")]
    public sealed class SandboxMappedFolder
    {
        public const string DefaultAssetPath = @"C:\assets";

        [XmlElement("HostFolder")]
        public string HostFolder { get; set; }

        [XmlElement("SandboxFolder")]
        public string SandboxFolder { get; set; }

        [XmlElement("ReadOnly")]
        public string ReadOnly { get; set; } = bool.TrueString;
    }
}
