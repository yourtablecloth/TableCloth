using System.Collections.Generic;
using System.Windows.Controls;
using TableCloth.Models;
using TableCloth.Models.Catalog;

namespace TableCloth.Components;

public interface INavigationService
{
    Frame FindNavigationFrameFromMainWindow();
    string GetPageFrameControlName();
    bool NavigateToCatalog(string searchKeyword);
    bool NavigateToDetail(string searchKeyword, CatalogInternetService selectedService, CommandLineArgumentModel? commandLineArgumentModel);
    bool NavigateToQuickStart();

    /// <summary>
    /// 딥링크(<c>tablecloth:</c>) 진입 전용. QuickStart 로 이동한 뒤 사용자의 조작 없이 곧바로
    /// 샌드박스를 실행한다. 상세 페이지를 거치지 않으므로 중간 화면이 없다.
    /// </summary>
    bool NavigateToQuickStartAndLaunch(IEnumerable<CatalogInternetService> services, string? targetUrl);
    void GoBack();
}