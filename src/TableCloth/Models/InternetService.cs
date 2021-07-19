using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using TableCloth.Resources;

namespace TableCloth.Models
{
    [Serializable, XmlType("InternetService")]
    public sealed class InternetService
	{
		public InternetService(string id, string displayName, InternetServiceCategory category, Uri homepageUrl, IEnumerable<PackageInformation> packages)
		{
			Id = id;
			DisplayName = displayName;
			Category = category;
			Url = homepageUrl;
			Packages = new (packages);
		}

		[XmlAttribute("Id")]
		public string Id { get; set; }

		[XmlAttribute("DisplayName")]
		public string DisplayName { get; set; }

		[XmlAttribute("Category")]
		public InternetServiceCategory Category { get; set; }

		[XmlAttribute("Url")]
		public Uri Url { get; set; }

		[XmlArray, XmlArrayItem(typeof(PackageInformation), ElementName = "Packages")]
		public List<PackageInformation> Packages { get; set; } = new();

		[XmlIgnore]
		public string CategoryDisplayName
			=> StringResources.InternetServiceCategory_DisplayText(Category);

		public override string ToString()
			=> StringResources.InternetService_DisplayText(this);
	}
}
