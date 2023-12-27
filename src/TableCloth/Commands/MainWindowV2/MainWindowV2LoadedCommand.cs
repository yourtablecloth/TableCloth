using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TableCloth.Components;
using TableCloth.Models;
using TableCloth.ViewModels;
using NavigationService = TableCloth.Components.NavigationService;

namespace TableCloth.Commands.MainWindowV2;

public sealed class MainWindowV2LoadedCommand : CommandBase
{
    public MainWindowV2LoadedCommand(
        NavigationService navigationService,
        VisualThemeManager visualThemeManager,
        CommandLineParser commandLineParser)
    {
        _navigationService = navigationService;
        _visualThemeManager = visualThemeManager;
        _commandLineParser = commandLineParser;
    }

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

        var args = App.Current.Arguments.ToArray();
        var hasArgs = args.Count() > 0;

        if (hasArgs)
        {
            var parsedArg = _commandLineParser.ParseForV2(args);

            if (parsedArg.SelectedServices.Count() < 1)
                return;

            _navigationService.NavigateToDetail(parsedArg);
        }
        else
            _navigationService.NavigateToCatalog(new CatalogPageArgumentModel() { SearchKeyword = string.Empty, });
    }
}
