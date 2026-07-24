using System.Text;
using TableCloth.Components;

namespace Spork.Components.Implementations
{
    public sealed class LicenseDescriptor : ILicenseDescriptor
    {
        // 이슈 #296(AOT): About 창의 OSS 라이선스 목록. 과거 구현은 Assembly.GetReferencedAssemblies() +
        // Assembly.Load + 커스텀 어트리뷰트 리플렉션이었으나 Native AOT 에서 PlatformNotSupportedException 을
        // 던진다(런타임 확인). 큐레이트된 정적 목록(ThirdPartyLicenses)으로 대체 — 리플렉션 없이 AOT 안전.
        public string GetLicenseDescriptions()
        {
            var buffer = new StringBuilder();

            foreach (var component in ThirdPartyLicenses.Components)
            {
                buffer.AppendLine(component.Title);
                buffer.AppendLine(component.Copyright);

                if (!string.IsNullOrWhiteSpace(component.RepositoryUrl))
                    buffer.AppendLine($"Source Repository: {component.RepositoryUrl}");

                buffer.AppendLine();
            }

            return buffer.ToString();
        }
    }
}
