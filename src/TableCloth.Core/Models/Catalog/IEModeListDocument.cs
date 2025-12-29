using System.Collections.Generic;

namespace TableCloth.Models.Catalog
{
    /// <summary>
    /// Internet Explorer 모드로 표시할 웹 사이트의 목록을 담는 XML 요소를 나타냅니다.
    /// </summary>
    public sealed class IEModeListDocument
    {
        /// <summary>
        /// Internet Explorer 모드로 표시할 웹 사이트의 목록
        /// </summary>
        public List<IEModeSite> Sites { get; set; } = new List<IEModeSite>();
    }
}
