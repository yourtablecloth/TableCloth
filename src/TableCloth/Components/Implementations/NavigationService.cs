using System;
using System.Windows;
using System.Windows.Controls;
using TableCloth.Models;
using TableCloth.Models.Catalog;

namespace TableCloth.Components;

public sealed class NavigationService(
    IApplicationService applicationService,
    IAppUserInterface appUserInterface) : INavigationService
{
    public string GetPageFrameControlName()
        => nameof(MainWindowV2.PageFrame);

    public Frame FindNavigationFrameFromMainWindow()
    {
        var frameName = GetPageFrameControlName();
        var mainWindow = applicationService.GetMainWindow();
        var pageFrame = mainWindow!.FindName(frameName) as Frame;

        if (pageFrame == null)
            throw new Exception($"There is no frame control named as '{pageFrame}'.");

        return pageFrame;
    }

    public bool NavigateToCatalog(string searchKeyword)
    {
        var frame = FindNavigationFrameFromMainWindow();
        var page = appUserInterface.CreateCatalogPage(searchKeyword);
        return frame.Navigate(page);
    }

    public bool NavigateToDetail(
        CatalogInternetService selectedService,
        CommandLineArgumentModel? commandLineArgumentModel)
    {
        var frame = FindNavigationFrameFromMainWindow();
        var page = appUserInterface.CreateDetailPage(selectedService, commandLineArgumentModel);
        return frame.Navigate(page);
    }
}
