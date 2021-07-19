using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace TableCloth.Models
{
    [XmlRoot("Configuration")]
    public sealed partial class WindowsSandboxConfiguration
    {
        [XmlElement("AudioInput")]
        public string AudioInput { get; set; }

        [XmlElement("VideoInput")]
        public string VideoInput { get; set; }

        [XmlElement("PrinterRedirection")]
        public string PrinterRedirection { get; set; }

        [XmlArray, XmlArrayItem(typeof(string), ElementName = "Command")]
        public List<string> LogonCommand { get; set; } = new();

        [XmlArray, XmlArrayItem(typeof(WindowsSandboxMappedFolder), ElementName = "MappedFolder")]
        public List<WindowsSandboxMappedFolder> MappedFolders { get; } = new();
    }
}
