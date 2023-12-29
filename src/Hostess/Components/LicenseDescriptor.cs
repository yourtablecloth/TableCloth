using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Hostess.Components
{
    internal static class LicenseDescriptor
    {
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
                .Where(x => !bclPublicKeyTokens.Any(y => y.SequenceEqual(x.GetPublicKeyToken() ?? Array.Empty<byte>())))
                .ToList();

            refList.Insert(0, asm.GetName());
            return refList;
        }

        public static string GetLicenseDescriptions()
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
                    buffer.AppendLine($@"Source Repository: {asmRepoUrl}");

                buffer.AppendLine();
            }

            return buffer.ToString();
        }
    }
}
