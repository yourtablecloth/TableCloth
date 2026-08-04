using System;
using System.Collections.Generic;
using System.Linq;
using TableCloth.Models;
using TableCloth.Models.Catalog;

namespace TableCloth.Components.Implementations;

/// <inheritdoc cref="IDeepLinkResolver"/>
public sealed class DeepLinkResolver(
    IResourceCacheManager resourceCacheManager) : IDeepLinkResolver
{
    public DeepLinkResolution Resolve(CommandLineArgumentModel? argumentModel)
    {
        if (argumentModel == null)
            return DeepLinkResolution.Unresolved;

        var catalog = resourceCacheManager.CatalogDocument;

        if (catalog == null)
            return DeepLinkResolution.Unresolved;

        var services = catalog.Services;

        // 딥링크는 `tablecloth:wooribank` 처럼 카탈로그와 대소문자가 다를 수 있다. 여기서 카탈로그의
        // 정식 Id 로 정규화해, 이후 단계(상세 페이지의 재조회, 샌드박스 구성, 게스트의 Spork)가 모두
        // 같은 값을 보게 한다. 하위 단계들은 Ordinal 비교라 정규화하지 않으면 조용히 빈 선택이 된다.
        var requestedServiceIds = argumentModel.SelectedServices?.ToArray() ?? Array.Empty<string>();
        var selectedServiceIds = services
            .Where(x => requestedServiceIds.Contains(x.Id, StringComparer.OrdinalIgnoreCase))
            .Select(x => x.Id)
            .ToArray();
        var acceptedTargetUrl = default(string);

        if (!string.IsNullOrWhiteSpace(argumentModel.TargetUrl))
        {
            var match = CatalogTargetUrlMatcher.Match(catalog, argumentModel.TargetUrl, selectedServiceIds);

            // 사이트 Id 판정은 매처가 하나로 끝낸다(동점이면 카탈로그 순서). 여기서는 결과만 받는다 —
            // URL 형식과 사이트 Id 형식의 차이는 "열 주소가 카탈로그 대표 URL 이냐 지정된 URL 이냐"뿐이다.
            if (match.IsAccepted)
            {
                selectedServiceIds = match.ServiceIds.ToArray();
                acceptedTargetUrl = match.AcceptedUrl;
            }
        }

        var selectedServices = services
            .Where(x => selectedServiceIds.Contains(x.Id))
            .ToArray();

        if (selectedServices.Length < 1)
            return DeepLinkResolution.Unresolved;

        return new DeepLinkResolution(
            selectedServices,
            selectedServiceIds,
            acceptedTargetUrl,
            argumentModel.LaunchImmediately);
    }
}
