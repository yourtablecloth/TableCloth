using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Spork.Components.Implementations
{
    public sealed class LicenseDescriptor : ILicenseDescriptor
    {
        // 이슈 #296(트림/AOT): About 창의 OSS 라이선스 목록 생성용. 트리밍 시 GetReferencedAssemblies() 는 실제로
        // 배포되는 어셈블리만 반환하므로 목록이 배포물과 일치한다(크래시 아님, 정보성 표시). IL2026 의도적 수용.
        [UnconditionalSuppressMessage("Trimming", "IL2026",
            Justification = "OSS 라이선스 목록은 정보성 표시. 트림 후 남은(배포되는) 어셈블리만 열거되는 것이 오히려 정확하며 크래시 없음.")]
        private IEnumerable<AssemblyName> GetReferencedThirdPartyAssemblies()
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

        public string GetLicenseDescriptions()
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
