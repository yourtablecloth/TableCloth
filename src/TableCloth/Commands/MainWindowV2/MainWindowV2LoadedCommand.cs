using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TableCloth.Components;
using TableCloth.Models.Catalog;
using TableCloth.ViewModels;
using NavigationService = TableCloth.Components.NavigationService;

namespace TableCloth.Commands.MainWindowV2;

public sealed class MainWindowV2LoadedCommand : CommandBase
{
    public MainWindowV2LoadedCommand(
        ResourceCacheManager resourceCacheManager,
        NavigationService navigationService,
        VisualThemeManager visualThemeManager,
        CommandLineParser commandLineParser)
    {
        _resourceCacheManager = resourceCacheManager;
        _navigationService = navigationService;
        _visualThemeManager = visualThemeManager;
        _commandLineParser = commandLineParser;
    }

    private readonly ResourceCacheManager _resourceCacheManager;
    private readonly NavigationService _navigationService;
    private readonly VisualThemeManager _visualThemeManager;
    private readonly CommandLineParser _commandLineParser;

    public override void Execute(object? parameter)
    {
        if (parameter is not MainWindowV2ViewModel viewModel)
            throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

        const string pageFrameName = "PageFrame";

        var mainWindow = Application.Current.MainWindow;
        var pageFrame = mainWindow.FindName(pageFrameName) as Frame;

        if (pageFrame == null)
            throw new Exception($"There is no frame control named as '{pageFrame}'.");

        _navigationService.Initialize(pageFrame);
        _visualThemeManager.ApplyAutoThemeChange(mainWindow);

        var parsedArg = _commandLineParser.ParseFromArgv();
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
