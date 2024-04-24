#nullable enable

using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace TableCloth.Models.WindowsSandbox
{
    [XmlRoot("Configuration")]
    public sealed partial class SandboxConfiguration
    {
        [XmlElement("Networking")]
        public string? Networking { get; set; }

        [XmlIgnore]
        public bool NetworkingSpecified => Networking != null;

        [XmlElement("AudioInput")]
        public string? AudioInput { get; set; }

        [XmlIgnore]
        public bool AudioInputSpecified => AudioInput != null;

        [XmlElement("VideoInput")]
        public string? VideoInput { get; set; }

        [XmlIgnore]
        public bool VideoInputSpecified => VideoInput != null;

        [XmlElement("vGPU")]
        public string? VirtualGpu { get; set; }

        [XmlIgnore]
        public bool VirtualGpuSpecified => VirtualGpu != null;

        [XmlElement("PrinterRedirection")]
        public string? PrinterRedirection { get; set; }

        [XmlIgnore]
        public bool PrinterRedirectionSpecified => PrinterRedirection != null;

        [XmlElement("ClipboardRedirection")]
        public string? ClipboardRedirection { get; set; }

        [XmlIgnore]
        public bool ClipboardRedirectionSpecified => ClipboardRedirection != null;

        [XmlElement("ProtectedClient")]
        public string? ProtectedClient { get; set; }

        [XmlIgnore]
        public bool ProtectedClientSpecified => ProtectedClient != null;

        [XmlArray, XmlArrayItem(typeof(string), ElementName = "Command")]
        public List<string> LogonCommand { get; set; } = new List<string>();

        [XmlIgnore]
        public bool LogonCommandSpecified => LogonCommand != null && LogonCommand.Count > 0;

        [XmlArray, XmlArrayItem(typeof(SandboxMappedFolder), ElementName = "MappedFolder")]
        public List<SandboxMappedFolder> MappedFolders { get; } = new List<SandboxMappedFolder>();

        [XmlIgnore]
        public bool MappedFoldersSpecified => MappedFolders != null && MappedFolders.Count > 0;

        [XmlElement("MemoryInMB")]
        public int? MemoryInMB { get; set; }

        [XmlIgnore]
        public bool MemoryInMBSpecified => MemoryInMB.HasValue;
    }
}
