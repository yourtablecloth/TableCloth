using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TableCloth.Components;

public sealed class LicenseDescriptor : ILicenseDescriptor
{
    public LicenseDescriptor(
        IResourceResolver resourceResolver)
    {
        _resourceResolver = resourceResolver;
    }

    private IResourceResolver _resourceResolver;

    private IEnumerable<AssemblyName> GetReferencedThirdPartyAssemblies()
    {
        var asm = Assembly.GetEntryAssembly()
            ?? throw new Exception("Cannot obtain entry assembly information.");

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

    public async Task<string> GetLicenseDescriptions()
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
                    if (Uri.TryCreate(asmRepoUrl, UriKind.Absolute, out var parsedAsmRepoUrl) &&
                        string.Equals("github.com", parsedAsmRepoUrl.Host, StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = parsedAsmRepoUrl.GetComponents(UriComponents.Path, UriFormat.UriEscaped).Split('/');
                        var ownerPart = parts.ElementAtOrDefault(0);
                        var repoNamePart = parts.ElementAtOrDefault(1);

                        if (!string.IsNullOrWhiteSpace(ownerPart) &&
                            !string.IsNullOrWhiteSpace(repoNamePart))
                        {
                            var licenseDescription = await _resourceResolver.GetLicenseDescriptionForGitHub(ownerPart, repoNamePart).ConfigureAwait(false);
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
