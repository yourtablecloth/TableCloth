using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Hostess
{
    internal static class RootDomainParser
    {
        private static readonly string[] entries = ((new UTF8Encoding(false).GetString(Properties.Resources.public_suffix_list)
            ?.Split(new char[] { '\r', '\n', }, StringSplitOptions.RemoveEmptyEntries)
            ?.Where(x => !string.IsNullOrWhiteSpace(x))
            ?.Where(x => !x.Trim().StartsWith("//", StringComparison.Ordinal))
            ) ?? Array.Empty<string>())
            .ToArray();

        public static string InferenceRootDomain(Uri testUrl)
        {
            if (testUrl == null || string.IsNullOrWhiteSpace(testUrl.Host))
                return null;

            var longestSuffix = string.Empty;
            var host = testUrl.Host;

            foreach (var eachEntry in entries)
            {
                if (!host.EndsWith(eachEntry))
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
