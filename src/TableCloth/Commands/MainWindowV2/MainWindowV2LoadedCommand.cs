using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TableCloth.Components;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.ViewModels;

namespace TableCloth.Commands.MainWindowV2;

public sealed class MainWindowV2LoadedCommand : ViewModelCommandBase<MainWindowV2ViewModel>
{
    public MainWindowV2LoadedCommand(
        Application application,
        IResourceCacheManager resourceCacheManager,
        INavigationService navigationService,
        IVisualThemeManager visualThemeManager,
        ICommandLineArguments commandLineArguments)
    {
        _application = application;
        _resourceCacheManager = resourceCacheManager;
        _navigationService = navigationService;
        _visualThemeManager = visualThemeManager;
        _commandLineArguments = commandLineArguments;
    }

    private readonly Application _application;
    private readonly IResourceCacheManager _resourceCacheManager;
    private readonly INavigationService _navigationService;
    private readonly IVisualThemeManager _visualThemeManager;
    private readonly ICommandLineArguments _commandLineArguments;

    public override void Execute(MainWindowV2ViewModel viewModel)
    {
        var mainWindow = _application.MainWindow;
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
