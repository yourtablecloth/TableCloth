using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace TableCloth.Models.Catalog
{
	/// <summary>
	/// Internet Explorer 모드로 표시할 웹 사이트의 목록을 담는 XML 요소를 나타냅니다.
	/// </summary>
    [Serializable, XmlRoot("IEModeList")]
	public sealed class IEModeListDocument
	{
		/// <summary>
		/// Internet Explorer 모드로 표시할 웹 사이트의 목록
		/// </summary>
		[XmlArray(ElementName = "Sites"), XmlArrayItem(typeof(IEModeSite), ElementName = "Site")]
		public List<IEModeSite> Sites { get; set; } = new List<IEModeSite>();
	}
}
