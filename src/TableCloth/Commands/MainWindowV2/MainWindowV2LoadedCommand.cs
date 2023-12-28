using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TableCloth.Components;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.ViewModels;
using NavigationService = TableCloth.Components.NavigationService;

namespace TableCloth.Commands.MainWindowV2;

public sealed class MainWindowV2LoadedCommand : CommandBase
{
    public MainWindowV2LoadedCommand(
        CatalogCacheManager catalogCacheManager,
        NavigationService navigationService,
        VisualThemeManager visualThemeManager,
        CommandLineParser commandLineParser)
    {
        _catalogCacheManager = catalogCacheManager;
        _navigationService = navigationService;
        _visualThemeManager = visualThemeManager;
        _commandLineParser = commandLineParser;
    }

    private readonly CatalogCacheManager _catalogCacheManager;
    private readonly NavigationService _navigationService;
    private readonly VisualThemeManager _visualThemeManager;
    private readonly CommandLineParser _commandLineParser;

    public override void Execute(object? parameter)
    {
        const string pageFrameName = "PageFrame";

        var mainWindow = Application.Current.MainWindow;
        var pageFrame = mainWindow.FindName(pageFrameName) as Frame;

        if (pageFrame == null)
            throw new Exception($"There is no frame control named as '{pageFrame}'.");

        _navigationService.Initialize(pageFrame);
        _visualThemeManager.ApplyAutoThemeChange(mainWindow);

        var parsedArg = _commandLineParser.ParseFromArgv();
        var services = _catalogCacheManager.CatalogDocument.Services;

        var commandLineSelectedService = default(CatalogInternetService);
        if (parsedArg != null && parsedArg.SelectedServices.Count() > 0)
        {
            commandLineSelectedService = services
                .Where(x => parsedArg.SelectedServices.Contains(x.Id))
                .FirstOrDefault();
        }

        if (commandLineSelectedService != null)
            _navigationService.NavigateToDetail(commandLineSelectedService, parsedArg);
        else
            _navigationService.NavigateToCatalog(string.Empty);
    }
}
