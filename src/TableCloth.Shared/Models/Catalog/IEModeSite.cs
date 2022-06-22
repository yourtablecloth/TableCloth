using System;
using System.Xml.Serialization;

namespace TableCloth.Models.Catalog
{
	/// <summary>
	/// Internet Explorer 모드로 표시할 개별 사이트의 정보를 담는 XML를 나타냅니다.
	/// </summary>
    [Serializable, XmlType]
	public sealed class IEModeSite
	{
		/// <summary>
		/// 도메인
		/// </summary>
		[XmlAttribute("Domain")]
		public string Domain { get; set; }

		/// <summary>
		/// 동작 모드
		/// </summary>
		[XmlAttribute("Mode")]
		public string Mode { get; set; }

		/// <summary>
		/// 브라우저 실행 방법
		/// </summary>
		[XmlAttribute("OpenIn")]
		public string OpenIn { get; set; }
	}
}
