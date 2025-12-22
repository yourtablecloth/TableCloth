using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using TableCloth.Commands;
using TableCloth.Commands.CatalogPage;
using TableCloth.Commands.Shared;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public partial class CatalogPageViewModelForDesigner : CatalogPageViewModel
{
    public IList<CatalogInternetService> ServicesForDesigner
        => DesignTimeResources.DesignTimeCatalogDocument.Services;
}

public partial class CatalogPageViewModel : ViewModelBase
{
    protected CatalogPageViewModel() { }

    public CatalogPageViewModel(
        CatalogPageLoadedCommand catalogPageLoadedCommand,
        CatalogPageItemSelectCommand catalogPageItemSelectCommand,
        AppRestartCommand appRestartCommand,
        AboutThisAppCommand aboutThisAppCommand,
        ShowDebugInfoCommand showDebugInfoCommand,
        CatalogPageItemFavoriteCommand catalogPageFavoriteCommand)
    {
        _catalogPageLoadedCommand = catalogPageLoadedCommand;
        _catalogPageItemSelectCommand = catalogPageItemSelectCommand;
        _appRestartCommand = appRestartCommand;
        _aboutThisAppCommand = aboutThisAppCommand;
        _showDebugInfoCommand = showDebugInfoCommand;
        _catalogPageFavoriteCommand = catalogPageFavoriteCommand;
    }

    [RelayCommand]
    private void CatalogPageLoaded()
    {
        _catalogPageLoadedCommand.Execute(this);
    }

    private CatalogPageLoadedCommand _catalogPageLoadedCommand = default!;

    [RelayCommand]
    private void CatalogPageItemSelect()
    {
        _catalogPageItemSelectCommand.Execute(this);
    }

    private CatalogPageItemSelectCommand _catalogPageItemSelectCommand = default!;

    [RelayCommand]
    private void AppRestart()
    {
        _appRestartCommand.Execute(this);
    }

    private AppRestartCommand _appRestartCommand = default!;

    [RelayCommand]
    private void AboutThisApp()
    {
        _aboutThisAppCommand.Execute(this);
    }

    private AboutThisAppCommand _aboutThisAppCommand = default!;

    [RelayCommand]
    private void ShowDebugInfo()
    {
        _showDebugInfoCommand.Execute(this);
    }

    private ShowDebugInfoCommand _showDebugInfoCommand = default!;

    [RelayCommand]
    private void CatalogPageFavorite()
    {
        _catalogPageFavoriteCommand.Execute(this);
    }

    private CatalogPageItemFavoriteCommand _catalogPageFavoriteCommand = default!;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedServiceCategory))]
    private CatalogInternetService? _selectedService;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasServices))]
    private IList<CatalogInternetService> _services = new List<CatalogInternetService>();

    [ObservableProperty]
    private bool _showFavoritesOnly = default;

    public CatalogInternetServiceCategory? SelectedServiceCategory
        => SelectedService?.Category;

    public bool HasServices
        => Services.Count > 0;
}
