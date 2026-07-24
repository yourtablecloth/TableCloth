using Avalonia.Controls;
using System.Collections.Generic;
using TableCloth.Models;
using TableCloth.Models.Catalog;

namespace TableCloth.Components.Implementations;

// 이슈 #296: WPF Frame/Page 네비게이션 → Avalonia ContentControl(PageHost) + UserControl 페이지 전환 + 백스택.
public sealed class NavigationService(
    IApplicationService applicationService,
    IAppUserInterface appUserInterface) : INavigationService
{
    private readonly Stack<Control> _backStack = new();

    private ContentControl? FindPageHost()
        => applicationService.GetMainWindow()?.FindControl<ContentControl>("PageHost");

    private bool NavigateTo(Control page)
    {
        var host = FindPageHost();
        if (host == null)
            return false;

        if (host.Content is Control current)
            _backStack.Push(current);

        host.Content = page;
        return true;
    }

    public bool NavigateToCatalog(string searchKeyword)
        => NavigateTo(appUserInterface.CreateCatalogPage(searchKeyword));

    public bool NavigateToDetail(
        string searchKeyword,
        CatalogInternetService selectedService,
        CommandLineArgumentModel? commandLineArgumentModel)
        => NavigateTo(appUserInterface.CreateDetailPage(searchKeyword, selectedService, commandLineArgumentModel));

    public bool NavigateToQuickStart()
        => NavigateTo(appUserInterface.CreateQuickStartPage());

    public void GoBack()
    {
        var host = FindPageHost();
        if (host != null && _backStack.Count > 0)
            host.Content = _backStack.Pop();
    }
}
