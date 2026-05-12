using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using TableCloth.Components;
using TableCloth.Models.Catalog;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public partial class MainWindowViewModelForDesigner : MainWindowViewModel { }

public partial class MainWindowViewModel : ObservableObject
{
    protected MainWindowViewModel() { }

    [ActivatorUtilitiesConstructor]
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

        // --select <SiteId> 하위 호환: 기존 사용자/바로가기가 깨지지 않도록 SiteId가 들어오면
        // 종전대로 DetailPage를 통해 진입한다. 그 외 일반 경우는 새 QuickStart 진입점을 사용한다.
        if (commandLineSelectedService != null)
            _navigationService.NavigateToDetail(string.Empty, commandLineSelectedService, parsedArg);
        else
            _navigationService.NavigateToQuickStart();
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
