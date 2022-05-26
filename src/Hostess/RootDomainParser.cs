using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Hostess
{
    internal static class RootDomainParser
    {
        private static readonly Lazy<IEnumerable<string>> entries = new Lazy<IEnumerable<string>>(InitializeEntries);

        private static IEnumerable<string> InitializeEntries()
        {
            var content = string.Empty;
            var result = new string[] { };

            try
            {
                using (var webClient = new WebClient())
                {
                    content = webClient.DownloadString("https://publicsuffix.org/list/public_suffix_list.dat");
                }
            }
            catch
            {
                // 최신 리스트를 가져올 수 없을 경우 로컬 사본으로 대체
                content = new UTF8Encoding(false).GetString(Properties.Resources.public_suffix_list);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(content))
                {
                    result = content
                        .Split(new char[] { '\r', '\n', }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Where(x => !x.Trim().StartsWith("//", StringComparison.Ordinal))
                        .ToArray();
                }
            }

            return result;
        }

        public static string InferenceRootDomain(Uri testUrl)
        {
            if (testUrl == null || string.IsNullOrWhiteSpace(testUrl.Host))
                return null;

            var longestSuffix = string.Empty;
            var host = testUrl.Host;

            foreach (var eachEntry in entries.Value)
            {
                var suffix = "." + eachEntry.TrimStart('.');
                if (!host.EndsWith(suffix))
                    continue;
                if (longestSuffix.Length < eachEntry.Length)
                    longestSuffix = eachEntry;
            }

            if (string.IsNullOrWhiteSpace(longestSuffix))
                return null;

            var trimmedHost = Regex.Replace(host, $"(.{longestSuffix})$", string.Empty);
            var lastPart = trimmedHost.Split(new char[] { '.', }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

            if (string.IsNullOrWhiteSpace(lastPart))
                return null;

            return $"{lastPart}.{longestSuffix}";
        }
    }
}
