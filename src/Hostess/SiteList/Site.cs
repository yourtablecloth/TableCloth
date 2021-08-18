using System;
using System.Xml.Serialization;

namespace Hostess.SiteList
{
    [Serializable, XmlType]
    public class Site
    {
        [XmlAttribute("url")]
        public string Url { get; set; }

        [XmlElement("compat-mode")]
        public string CompatibilityMode { get; set; } = "Default";

        [XmlElement("open-in")]
        public string OpenIn { get; set; } = "IE11";
    }
}
