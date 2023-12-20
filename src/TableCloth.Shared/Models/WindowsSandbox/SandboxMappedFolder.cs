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
        public string HostFolder { get; set; } = string.Empty;

        // https://docs.microsoft.com/en-us/windows/whats-new/whats-new-windows-10-version-2004#virtualization
        [XmlElement("SandboxFolder")]
        public string
#if !NETFX
            ?
#endif
            SandboxFolder
        { get; set; }

        [XmlElement("ReadOnly")]
        public string
#if !NETFX
            ?
#endif
            ReadOnly
        { get; set; }

        public bool ShouldSerializeSandboxFolder()
            => !string.IsNullOrWhiteSpace(SandboxFolder);

        public bool ShouldSerializeReadOnly()
            => !string.IsNullOrWhiteSpace(ReadOnly);
    }
}
