using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Resources;

namespace TableCloth.Components.Implementations;

public sealed class LicenseDescriptor(
    IResourceResolver resourceResolver,
    ILogger<LicenseDescriptor> logger) : ILicenseDescriptor
{
    // 이슈 #296(트림/AOT): About 창의 OSS 라이선스 목록 생성용. 트리밍 시 GetReferencedAssemblies() 는 실제로
    // 포함(배포)된 어셈블리만 반환하므로, 목록은 '실제 배포되는 것'과 일치한다(누락 = 트림으로 제거되어 배포되지 않음).
    // 크래시가 아니라 정보성 표시의 완전성 문제이므로 의도적으로 수용한다(IL2026 억제).
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "OSS 라이선스 목록은 정보성 표시. 트림 후 남은(배포되는) 어셈블리만 열거되는 것이 오히려 정확하며 크래시 없음.")]
    private static AssemblyName[] GetReferencedThirdPartyAssemblies()
    {
        var asm = Assembly.GetEntryAssembly();
        ArgumentNullException.ThrowIfNull(asm);

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
                            if (licenseDescription.Result != null)
                                buffer.AppendLine($"OSS License: {licenseDescription.Result}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // GitHub API 호출 실패는 라이선스 설명 출력 자체를 막지 않도록 무시한다.
                    logger.LogDebug(ex, "Failed to fetch GitHub license description for {RepoUrl}.", asmRepoUrl);
                }
            }

            buffer.AppendLine();
        }

        return buffer.ToString();
    }
}
