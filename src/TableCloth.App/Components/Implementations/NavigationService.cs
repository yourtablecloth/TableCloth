using System;
using System.Collections.Generic;
using System.Windows.Controls;
using TableCloth.Models;
using TableCloth.Models.Catalog;

namespace TableCloth.Components.Implementations;

public sealed class NavigationService(
    IApplicationService applicationService,
    IAppUserInterface appUserInterface) : INavigationService
{
    public string GetPageFrameControlName()
        => nameof(MainWindow.PageFrame);

    public Frame FindNavigationFrameFromMainWindow()
    {
        var frameName = GetPageFrameControlName();
        var mainWindow = applicationService.GetMainWindow();

        var pageFrame = mainWindow!.FindName(frameName) as Frame;
        ArgumentNullException.ThrowIfNull(pageFrame);
        return pageFrame;
    }

    public bool NavigateToCatalog(string searchKeyword)
    {
        var frame = FindNavigationFrameFromMainWindow();
        var page = appUserInterface.CreateCatalogPage(searchKeyword);
        return frame.Navigate(page);
    }

    public bool NavigateToDetail(
        string searchKeyword,
        CatalogInternetService selectedService,
        CommandLineArgumentModel? commandLineArgumentModel)
    {
        var frame = FindNavigationFrameFromMainWindow();
        var page = appUserInterface.CreateDetailPage(searchKeyword, selectedService, commandLineArgumentModel);
        return frame.Navigate(page);
    }

    public bool NavigateToQuickStart()
    {
        var frame = FindNavigationFrameFromMainWindow();
        var page = appUserInterface.CreateQuickStartPage();
        return frame.Navigate(page);
    }

    public bool NavigateToQuickStartAndLaunch(IEnumerable<CatalogInternetService> services, string? targetUrl)
    {
        var frame = FindNavigationFrameFromMainWindow();
        var page = appUserInterface.CreateQuickStartPage();

        // 페이지의 Loaded 커맨드가 이 값들을 보고 곧바로 실행한다(중간 화면·조작 없음).
        page.ViewModel.PreselectedServices = services ?? Array.Empty<CatalogInternetService>();
        page.ViewModel.PreselectedTargetUrl = targetUrl;
        page.ViewModel.LaunchImmediately = true;

        return frame.Navigate(page);
    }

    public void GoBack()
    {
        var frame = FindNavigationFrameFromMainWindow();

        if (frame.CanGoBack)
            frame.GoBack();
    }
}
