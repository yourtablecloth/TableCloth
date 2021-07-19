using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using TableCloth.Resources;

namespace TableCloth.Models.TableClothCatalog
{
    [Serializable, XmlType]
    public sealed class CatalogInternetService
    {
        [XmlAttribute("Id")]
        public string Id { get; set; }

        [XmlAttribute("DisplayName")]
        public string DisplayName { get; set; }

        [XmlAttribute("Category")]
        public CatalogInternetServiceCategory Category { get; set; }

        [XmlAttribute("Url")]
        public string Url { get; set; }

        [XmlElement("CompatNotes")]
        public string CompatibilityNotes { get; set; }

        [XmlArray, XmlArrayItem(typeof(CatalogPackageInformation), ElementName = "Packages")]
        public List<CatalogPackageInformation> Packages { get; set; } = new();

        [XmlIgnore]
        public string CategoryDisplayName
            => StringResources.InternetServiceCategory_DisplayText(Category);

        public override string ToString()
            => StringResources.InternetService_DisplayText(this);
    }
}
