using System;
using System.Collections.Generic;

namespace TableCloth.Models
{
    public sealed class InternetService
	{
		public InternetService(string siteName, Uri homepageUrl, IEnumerable<KeyValuePair<string, string>> packages)
		{
			SiteName = siteName;
			HomepageUrl = homepageUrl;
			Packages = packages;
		}

		public string SiteName { get; private set; }
		public Uri HomepageUrl { get; private set; }
		public IEnumerable<KeyValuePair<string, string>> Packages { get; private set; }

		public override string ToString() => $"{SiteName} - {HomepageUrl}";
	}
}
