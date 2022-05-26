using System;
using System.Xml.Serialization;

namespace TableCloth.Models.Catalog
{
    [Serializable, XmlType]
	public sealed class IEModeSite
	{
		[XmlAttribute("Domain")]
		public string Domain { get; set; }

		[XmlAttribute("Mode")]
		public string Mode { get; set; }

		[XmlAttribute("OpenIn")]
		public string OpenIn { get; set; }
	}
}
