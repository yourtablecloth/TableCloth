using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Hostess.SiteList
{
    [Serializable, XmlRoot("site-list")]
    public class SiteListDocument
    {
        [XmlAttribute("version")]
        public string Version { get; set; } = "1";

        [XmlElement("created-by")]
        public CreatedBy CreatedBy { get; set; } = new CreatedBy();

        [XmlElement("site")]
        public List<Site> Sites { get; set; } = new List<Site>();
    }
}
