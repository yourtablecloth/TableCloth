using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Components;
using TableCloth.Resources;

namespace TableCloth.Components.Implementations;

public sealed class LicenseDescriptor(
    IResourceResolver resourceResolver,
    ILogger<LicenseDescriptor> logger) : ILicenseDescriptor
{
    // 이슈 #296(AOT): About 창의 OSS 라이선스 목록. 과거 구현은 Assembly.GetReferencedAssemblies() +
    // Assembly.Load + 커스텀 어트리뷰트 리플렉션이었으나 Native AOT 에서 PlatformNotSupportedException 을
    // 던진다(런타임 확인). 큐레이트된 정적 목록(ThirdPartyLicenses)으로 대체하되, GitHub 저장소가 있는
    // 항목은 기존처럼 GitHub API 로 라이선스 종류를 조회해 덧붙인다(네트워크 경로는 AOT 안전).
    public async Task<string> GetLicenseDescriptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var buffer = new StringBuilder();

        foreach (var component in ThirdPartyLicenses.Components)
        {
            buffer.AppendLine(component.Title);
            buffer.AppendLine(component.Copyright);

            if (!string.IsNullOrWhiteSpace(component.RepositoryUrl))
            {
                buffer.AppendLine($"Source Repository: {component.RepositoryUrl}");

                try
                {
                    if (Uri.TryCreate(component.RepositoryUrl, UriKind.Absolute, out var repoUri) &&
                        string.Equals(ConstantStrings.GitHub_Domain, repoUri.Host, StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = repoUri.GetComponents(UriComponents.Path, UriFormat.UriEscaped).Split('/');
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
                    logger.LogDebug(ex, "Failed to fetch GitHub license description for {RepoUrl}.", component.RepositoryUrl);
                }
            }

            buffer.AppendLine();
        }

        return buffer.ToString();
    }
}
