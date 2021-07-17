using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace TableCloth.Models
{
	[Serializable]
    public sealed class InternetService
	{
		public static string GetCategoryDisplayName(InternetServiceCategory value)
        {
			var categoryType = typeof(InternetServiceCategory);
			var memberInfos = categoryType.GetMember(value.ToString());

			if (memberInfos
				?.FirstOrDefault()
				?.GetCustomAttributes(typeof(DescriptionAttribute), false)
				?.FirstOrDefault() is not DescriptionAttribute attribute)
				return "알 수 없음";

			return attribute?.Description ?? "알 수 없음";
		}

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

		public string CategoryDisplayName
			=> GetCategoryDisplayName(Category);

		public override string ToString()
        {
			var defaultString = $"{SiteName} - {HomepageUrl}";

			if (Packages != null && Packages.Count() > 0)
				defaultString = $"{defaultString} (총 {Packages.Count()}개 프로그램 설치)";

            return defaultString;
		}
	}
}
