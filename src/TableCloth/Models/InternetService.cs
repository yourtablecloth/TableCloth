using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace TableCloth.Models
{
	[Serializable]
    public sealed class InternetService
	{
		public InternetService(string siteName, InternetServiceCategory category, Uri homepageUrl, IEnumerable<PackageInformation> packages)
		{
			SiteName = siteName;
			Category = category;
			HomepageUrl = homepageUrl;
			Packages = packages;
		}

		public string SiteName { get; init; }
		public InternetServiceCategory Category { get; init; }
		public Uri HomepageUrl { get; init; }
		public IEnumerable<PackageInformation> Packages { get; init; }

		public override string ToString()
        {
			var categoryType = typeof(InternetServiceCategory);
			var memberInfos = categoryType.GetMember(Category.ToString());
			var defaultString = $"{SiteName} - {HomepageUrl}";

			if (Packages != null && Packages.Count() > 0)
				defaultString = $"{defaultString} (총 {Packages.Count()}개 프로그램 설치)";

            if (memberInfos
                ?.FirstOrDefault()
                ?.GetCustomAttributes(typeof(DescriptionAttribute), false)
                ?.FirstOrDefault() is not DescriptionAttribute attribute)
                return defaultString;

            return $"[{attribute.Description}] {defaultString}";
		}
	}
}
