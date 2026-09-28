using System.Collections.Generic;
using TableCloth.Models;
using TableCloth.Models.Catalog;

namespace TableCloth.Components;

public interface INavigationService
{
    bool NavigateToCatalog(string searchKeyword);
    bool NavigateToDetail(string searchKeyword, CatalogInternetService selectedService, CommandLineArgumentModel? commandLineArgumentModel);
    bool NavigateToQuickStart();

    /// <summary>
    /// 선택한 카탈로그 서비스 또는 딥링크 대상으로 QuickStart를 열고 샌드박스를 실행한다.
    /// QuickStart의 Data, NPKI 및 사용자 폴더 설정을 그대로 적용한다.
    /// </summary>
    bool NavigateToQuickStartAndLaunch(IEnumerable<CatalogInternetService> services, string? targetUrl);
    void GoBack();
}
