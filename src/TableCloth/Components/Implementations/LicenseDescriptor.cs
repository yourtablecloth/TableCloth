﻿using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Resources;

namespace TableCloth.Components.Implementations;

public sealed class LicenseDescriptor(
    IResourceResolver resourceResolver) : ILicenseDescriptor
{
    private static AssemblyName[] GetReferencedThirdPartyAssemblies()
    {
        var asm = Assembly.GetEntryAssembly().EnsureNotNull(ErrorStrings.Error_Cannot_Obtain_Assembly);

        var bclPublicKeyTokens = new byte[][] {
            [0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a,],
            [0x31, 0xbf, 0x38, 0x56, 0xad, 0x36, 0x4e, 0x35,],
            [0xb7, 0x7a, 0x5c, 0x56, 0x19, 0x34, 0xe0, 0x89,],
            [0xad, 0xb9, 0x79, 0x38, 0x29, 0xdd, 0xae, 0x60,],
            [0xcc, 0x7b, 0x13, 0xff, 0xcd, 0x2d, 0xdd, 0x51,],
        };

        var refList = asm
            .GetReferencedAssemblies()
            .Prepend(asm.GetName())
            .Where(x => !bclPublicKeyTokens.Any(y => y.SequenceEqual(x.GetPublicKeyToken() ?? [])))
            .ToArray();

        return refList;
    }

    public async Task<string> GetLicenseDescriptionsAsync(
        CancellationToken cancellationToken = default)
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
                        string.Equals(ConstantStrings.GitHub_Domain, parsedAsmRepoUrl.Host, StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = parsedAsmRepoUrl.GetComponents(UriComponents.Path, UriFormat.UriEscaped).Split('/');
                        var ownerPart = parts.ElementAtOrDefault(0);
                        var repoNamePart = parts.ElementAtOrDefault(1);

                        if (!string.IsNullOrWhiteSpace(ownerPart) &&
                            !string.IsNullOrWhiteSpace(repoNamePart))
                        {
                            var licenseDescription = await resourceResolver.GetLicenseDescriptionForGitHubAsync(ownerPart, repoNamePart, cancellationToken).ConfigureAwait(false);
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
