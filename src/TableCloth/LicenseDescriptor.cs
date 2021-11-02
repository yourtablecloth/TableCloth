using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TableCloth
{
    internal static class LicenseDescriptor
    {
        private static readonly Lazy<HttpClient> _httpClientFactory = new Lazy<HttpClient>(() =>
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/95.0.4638.69 Safari/537.36");
            return client;

        }, true);

        private static IEnumerable<AssemblyName> GetReferencedThirdPartyAssemblies()
        {
            var asm = Assembly.GetEntryAssembly();

            var bclPublicKeyTokens = new byte[][] {
                new byte[] { 0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a, },
                new byte[] { 0x31, 0xbf, 0x38, 0x56, 0xad, 0x36, 0x4e, 0x35, },
                new byte[] { 0xb7, 0x7a, 0x5c, 0x56, 0x19, 0x34, 0xe0, 0x89, },
                new byte[] { 0xad, 0xb9, 0x79, 0x38, 0x29, 0xdd, 0xae, 0x60, },
                new byte[] { 0xcc, 0x7b, 0x13, 0xff, 0xcd, 0x2d, 0xdd, 0x51, },
            };

            var refList = asm
                .GetReferencedAssemblies()
                .Prepend(asm.GetName())
                .Where(x => !bclPublicKeyTokens.Any(y => y.SequenceEqual(x.GetPublicKeyToken() ?? Array.Empty<byte>())))
                .ToArray();

            return refList;
        }

        private static async Task<string?> GetLicenseDescriptionForGitHub(string owner, string repoName)
        {
            var targetUri = new Uri($"https://api.github.com/repos/{owner}/{repoName}/license", UriKind.Absolute);
            var httpClient = _httpClientFactory.Value;

            using var licenseDescription = await httpClient.GetStreamAsync(targetUri);
            var jsonDocument = await JsonDocument.ParseAsync(licenseDescription).ConfigureAwait(false);
            return jsonDocument.RootElement.GetProperty("license").GetProperty("name").GetString();
        }

        public static async Task<string> GetLicenseDescriptions()
        {
            var buffer = new StringBuilder();

            foreach (var eachAsm in GetReferencedThirdPartyAssemblies())
            {
                var asm = Assembly.Load(eachAsm);
                var asmProduct = asm.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
                var asmRepoUrl = asm.GetCustomAttributes<AssemblyMetadataAttribute>()
                    ?.Where(x => string.Equals("RepositoryUrl", x.Key, StringComparison.OrdinalIgnoreCase))
                    ?.Select(x => x.Value)
                    ?.FirstOrDefault();
                var asmCompany = asm.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
                var asmCopyright = asm.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
                var asmTitle = asm.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;
                var asmTrademark = asm.GetCustomAttribute<AssemblyTrademarkAttribute>()?.Trademark;
                var asmVersion = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

                buffer.AppendLine($@"{asmTitle} {asmVersion} (Product of {asmProduct})
(c) {asmCompany} {asmTrademark}, All rights reserved.");

                if (asmRepoUrl != null)
                {
                    buffer.AppendLine($@"Source Repository: {asmRepoUrl}");

                    try
                    {
                        if (Uri.TryCreate(asmRepoUrl, UriKind.Absolute, out Uri parsedAsmRepoUrl) &&
                            string.Equals("github.com", parsedAsmRepoUrl.Host, StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = parsedAsmRepoUrl.GetComponents(UriComponents.Path, UriFormat.UriEscaped).Split('/');
                            var ownerPart = parts.ElementAtOrDefault(0);
                            var repoNamePart = parts.ElementAtOrDefault(1);

                            if (!string.IsNullOrWhiteSpace(ownerPart) &&
                                !string.IsNullOrWhiteSpace(repoNamePart))
                            {
                                var licenseDescription = await GetLicenseDescriptionForGitHub(ownerPart, repoNamePart).ConfigureAwait(false);
                                if (licenseDescription != null)
                                    buffer.AppendLine($"OSS License: {licenseDescription}");
                            }
                        }
                    }
                    catch { /* Ignore errors */ }
                }

                buffer.AppendLine();
            }

            return buffer.ToString();
        }
    }
}
