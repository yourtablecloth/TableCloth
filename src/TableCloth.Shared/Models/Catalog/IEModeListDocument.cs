using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace TableCloth.Models.Catalog
{
    [Serializable, XmlRoot("IEModeList")]
	public sealed class IEModeListDocument
	{
		[XmlArray(ElementName = "Sites"), XmlArrayItem(typeof(IEModeSite), ElementName = "Site")]
		public List<IEModeSite> Sites { get; set; } = new List<IEModeSite>();
	}
}
