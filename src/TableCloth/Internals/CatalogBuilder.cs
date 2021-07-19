using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TableCloth.Helpers;
using TableCloth.Models.TableClothCatalog;
using TableCloth.Resources;

namespace TableCloth.Internals
{
    static class CatalogBuilder
    {
		internal static IEnumerable<CatalogInternetService> ParseCatalog(string iniFilePath, bool addDefaultItem = true)
		{
			if (!File.Exists(iniFilePath))
				return Array.Empty<CatalogInternetService>();

			var parser = new IniFileParser(iniFilePath);
			var items = new List<CatalogInternetService>();

			foreach (var eachSiteSection in parser.GetSectionNames())
			{
				var pairs = parser.GetSectionValues(eachSiteSection);
				var siteName = pairs.Where(x => string.Equals(x.Key, "SiteName", StringComparison.OrdinalIgnoreCase)).Select(x => x.Value).FirstOrDefault();
				_ = Enum.TryParse(pairs.Where(x => string.Equals(x.Key, "Category", StringComparison.OrdinalIgnoreCase)).Select(x => x.Value).FirstOrDefault(), out CatalogInternetServiceCategory category);
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

				var packages = new PackageCollection();
				var appPrefixRegex = new Regex("App_", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
				var argPrefixRegex = new Regex("Arg_", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

				foreach (var eachPair in pairs)
                {
					if (appPrefixRegex.IsMatch(eachPair.Key))
                    {
						var packageName = appPrefixRegex.Replace(eachPair.Key, string.Empty);
						var package = default(CatalogPackageInformation);

						if (!packages.Contains(packageName))
							packages.Add(package = new CatalogPackageInformation() { Name = packageName });
						else
							package = packages[packageName];

						if (!Uri.TryCreate(eachPair.Value, UriKind.Absolute, out Uri packageUri))
							continue;

						package.Url = packageUri.ToString();
                    }
					else if (argPrefixRegex.IsMatch(eachPair.Key))
					{
						var packageName = argPrefixRegex.Replace(eachPair.Key, string.Empty);
						var package = default(CatalogPackageInformation);

						if (!packages.Contains(packageName))
							packages.Add(package = new CatalogPackageInformation() { Name = packageName });
						else
							package = packages[packageName];

						package.Arguments = (eachPair.Value ?? string.Empty).Trim();
					}
                }

				items.Add(new CatalogInternetService()
				{
					Id = eachSiteSection,
					DisplayName = siteName,
					Category = category,
					Url = homePageUrl.ToString(),
					Packages = packages.Where(x => x.Url != null).ToList(),
				});
			}

			if (addDefaultItem)
			{
				items.Add(new CatalogInternetService()
                {
					Id = string.Empty,
					DisplayName = StringResources.MainForm_JustRunItemText,
					Category = default,
					Url = "https://www.naver.com/",
					Packages = new(),
				});
			}

			return items.ToArray();
		}
	}
}
