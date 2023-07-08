using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace TableCloth.Models.WindowsSandbox
{
    [XmlRoot("Configuration")]
    public sealed partial class SandboxConfiguration
    {
        [XmlElement("AudioInput")]
        public string AudioInput { get; set; }

        [XmlElement("VideoInput")]
        public string VideoInput { get; set; }

        [XmlElement("PrinterRedirection")]
        public string PrinterRedirection { get; set; }

        [XmlArray, XmlArrayItem(typeof(string), ElementName = "Command")]
        public List<string> LogonCommand { get; set; } = new List<string>();

        [XmlArray, XmlArrayItem(typeof(SandboxMappedFolder), ElementName = "MappedFolder")]
        public List<SandboxMappedFolder> MappedFolders { get; } = new List<SandboxMappedFolder>();
    }
}
