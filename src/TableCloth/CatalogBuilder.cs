using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TableCloth.Helpers;
using TableCloth.Models;

namespace TableCloth
{
    static class CatalogBuilder
    {
		internal static IEnumerable<InternetService> ParseCatalog(string iniFilePath, bool addDefaultItem = true)
		{
			var parser = new IniFileParser(iniFilePath);
			var items = new List<InternetService>();

			foreach (var eachSiteSection in parser.GetSectionNames())
			{
				var pairs = parser.GetSectionValues(eachSiteSection);
				var siteName = pairs.Where(x => string.Equals(x.Key, "SiteName", StringComparison.OrdinalIgnoreCase)).Select(x => x.Value).FirstOrDefault();
				var webSiteUrl = pairs.Where(x => string.Equals(x.Key, "WebSiteUrl", StringComparison.OrdinalIgnoreCase)).Select(x => x.Value).FirstOrDefault();
				
				if (string.IsNullOrWhiteSpace(siteName) ||
					string.IsNullOrWhiteSpace(webSiteUrl))
				{
					continue;
				}

				Uri homePageUrl;
				if (!Uri.TryCreate(webSiteUrl, UriKind.Absolute, out homePageUrl) ||
					(!homePageUrl.Scheme.Equals(Uri.UriSchemeHttps) && !homePageUrl.Scheme.Equals(Uri.UriSchemeHttp)))
				{
					continue;
				}

				var regex = new Regex("App_", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
				var packageList = pairs
					.Where(x => x.Key.StartsWith("App_", StringComparison.OrdinalIgnoreCase))
					.Select(x => new KeyValuePair<string, string>(regex.Replace(x.Key, string.Empty), x.Value))
					.ToArray();
				items.Add(new InternetService(siteName, homePageUrl, packageList));
			}

			if (addDefaultItem)
			{
				items.Add(new InternetService(
					"그냥 실행해주세요.",
					new Uri("https://www.naver.com/"),
					new KeyValuePair<string, string>[] { }));
			}

			return items.ToArray();
		}
	}
}
