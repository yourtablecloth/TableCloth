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

public sealed class MainWindowV2LoadedCommand : ViewModelCommandBase<MainWindowV2ViewModel>
{
    public MainWindowV2LoadedCommand(
        Application application,
        ResourceCacheManager resourceCacheManager,
        NavigationService navigationService,
        VisualThemeManager visualThemeManager,
        CommandLineArguments commandLineArguments)
    {
        _application = application;
        _resourceCacheManager = resourceCacheManager;
        _navigationService = navigationService;
        _visualThemeManager = visualThemeManager;
        _commandLineArguments = commandLineArguments;
    }

    private readonly Application _application;
    private readonly ResourceCacheManager _resourceCacheManager;
    private readonly NavigationService _navigationService;
    private readonly VisualThemeManager _visualThemeManager;
    private readonly CommandLineArguments _commandLineArguments;

    public override void Execute(MainWindowV2ViewModel viewModel)
    {
        const string pageFrameName = "PageFrame";

        var mainWindow = _application.MainWindow;
        var pageFrame = mainWindow.FindName(pageFrameName) as Frame;

        if (pageFrame == null)
            throw new Exception($"There is no frame control named as '{pageFrame}'.");

        _navigationService.Initialize(pageFrame);
        _visualThemeManager.ApplyAutoThemeChange(mainWindow);

        var parsedArg = _commandLineArguments.Current;
        var services = _resourceCacheManager.CatalogDocument.Services;

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
