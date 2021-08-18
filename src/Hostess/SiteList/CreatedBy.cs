using System;
using System.Xml.Serialization;

namespace Hostess.SiteList
{
    [Serializable, XmlType]
    public class CreatedBy
    {
        [XmlElement("tool")]
        public string ToolName { get; set; } = "EMIESiteListManager";

        [XmlElement("version")]
        public string Version { get; set; } = "10.0.14357.1004";

        [XmlElement("date-created")]
        public string DateCreated { get; set; } = DateTime.UtcNow.ToString("MM/dd/yyyy HH:mm:ss");
    }
}
