using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace TableCloth.Models.Catalog
{
    /// <summary>
    /// 식탁보에서 실행할 서비스와 연관된 소프트웨어들의 정보를 표현하는 XML 요소를 나타냅니다.
    /// </summary>
    [Serializable, XmlRoot("TableClothCatalog")]
    public class CatalogDocument
    {
        /// <summary>
        /// 서비스에 관계없이 공용으로 사용할 수 있는 소프트웨어에 대한 정보 목록
        /// </summary>
        [XmlArray(ElementName = "Companions"), XmlArrayItem(typeof(CatalogCompanion), ElementName = "Companion")]
        public List<CatalogCompanion> Companions { get; set; } = new List<CatalogCompanion>();

        /// <summary>
        /// 특정 서비스 및 해당 서비스용 소프트웨어에 대한 정보 목록
        /// </summary>
        [XmlArray(ElementName = "InternetServices"), XmlArrayItem(typeof(CatalogInternetService), ElementName = "Service")]
        public List<CatalogInternetService> Services { get; set; } = new List<CatalogInternetService>();
    }
}
