using CommunityToolkit.Mvvm.Input;
using System;
using System.Linq;
using TableCloth.Components;
using TableCloth.Models.Catalog;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public partial class MainWindowViewModelForDesigner : MainWindowViewModel { }

public partial class MainWindowViewModel : ViewModelBase
{
    protected MainWindowViewModel() { }

    public MainWindowViewModel(
        IApplicationService applicationService,
        IResourceCacheManager resourceCacheManager,
        INavigationService navigationService,
        ICommandLineArguments commandLineArguments,
        ISandboxCleanupManager sandboxCleanupManager,
        IAppRestartManager appRestartManager)
    {
        _applicationService = applicationService;
        _resourceCacheManager = resourceCacheManager;
        _navigationService = navigationService;
        _commandLineArguments = commandLineArguments;
        _sandboxCleanupManager = sandboxCleanupManager;
        _appRestartManager = appRestartManager;
    }

    [RelayCommand]
    private void MainWindowLoaded()
    {
        _applicationService.ApplyCosmeticChangeToMainWindow();

        var parsedArg = _commandLineArguments.GetCurrent();
        var services = _resourceCacheManager.CatalogDocument.Services;

        var commandLineSelectedService = default(CatalogInternetService);
        if (parsedArg != null && parsedArg.SelectedServices.Any())
        {
            commandLineSelectedService = services
                .Where(x => parsedArg.SelectedServices.Contains(x.Id))
                .FirstOrDefault();
        }

        if (commandLineSelectedService != null)
            _navigationService.NavigateToDetail(string.Empty, commandLineSelectedService, parsedArg);
        else
            _navigationService.NavigateToCatalog(string.Empty);
    }

    [RelayCommand]
    private void MainWindowClosed()
    {
        _sandboxCleanupManager.TryCleanup();

        if (_appRestartManager.IsRestartReserved())
            _appRestartManager.RestartNow();
    }

    private readonly IApplicationService _applicationService = default!;
    private readonly IResourceCacheManager _resourceCacheManager = default!;
    private readonly INavigationService _navigationService = default!;
    private readonly ICommandLineArguments _commandLineArguments = default!;
    private readonly ISandboxCleanupManager _sandboxCleanupManager = default!;
    private readonly IAppRestartManager _appRestartManager = default!;
}
