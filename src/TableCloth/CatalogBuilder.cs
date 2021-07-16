using System;
using System.Collections.Generic;
using System.IO;
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
			if (!File.Exists(iniFilePath))
				return Array.Empty<InternetService>();

			var parser = new IniFileParser(iniFilePath);
			var items = new List<InternetService>();

			foreach (var eachSiteSection in parser.GetSectionNames())
			{
				var pairs = parser.GetSectionValues(eachSiteSection);
				var siteName = pairs.Where(x => string.Equals(x.Key, "SiteName", StringComparison.OrdinalIgnoreCase)).Select(x => x.Value).FirstOrDefault();
				_ = Enum.TryParse(pairs.Where(x => string.Equals(x.Key, "Category", StringComparison.OrdinalIgnoreCase)).Select(x => x.Value).FirstOrDefault(), out InternetServiceCategory category);
				var webSiteUrl = pairs.Where(x => string.Equals(x.Key, "WebSiteUrl", StringComparison.OrdinalIgnoreCase)).Select(x => x.Value).FirstOrDefault();
				
				if (string.IsNullOrWhiteSpace(siteName) ||
					string.IsNullOrWhiteSpace(webSiteUrl))
				{
					continue;
				}

                if (!Uri.TryCreate(webSiteUrl, UriKind.Absolute, out Uri homePageUrl) ||
                    (!homePageUrl.Scheme.Equals(Uri.UriSchemeHttps) && !homePageUrl.Scheme.Equals(Uri.UriSchemeHttp)))
                {
                    continue;
                }

                var regex = new Regex("App_", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
				var packageList = pairs
					.Where(x => x.Key.StartsWith("App_", StringComparison.OrdinalIgnoreCase))
					.Select(x => new KeyValuePair<string, string>(regex.Replace(x.Key, string.Empty), x.Value))
					.ToArray();
				items.Add(new InternetService(siteName, category, homePageUrl, packageList));
			}

			if (addDefaultItem)
			{
				items.Add(new InternetService(
					"그냥 실행해주세요.",
					default,
					new Uri("https://www.naver.com/"),
                    Array.Empty<KeyValuePair<string, string>>()));
			}

			return items.ToArray();
		}
	}
}
