using System;
using System.Collections.Generic;
using TableCloth.Commands;
using TableCloth.Commands.CatalogPage;
using TableCloth.Commands.Shared;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public class CatalogPageViewModelForDesigner : CatalogPageViewModel
{
    public IList<CatalogInternetService> ServicesForDesigner
        => DesignTimeResources.DesignTimeCatalogDocument.Services;
}

public class CatalogPageViewModel : ViewModelBase
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

    private readonly CatalogPageLoadedCommand _catalogPageLoadedCommand = default!;
    private readonly CatalogPageItemSelectCommand _catalogPageItemSelectCommand = default!;
    private readonly AppRestartCommand _appRestartCommand = default!;
    private readonly AboutThisAppCommand _aboutThisAppCommand = default!;
    private readonly ShowDebugInfoCommand _showDebugInfoCommand = default!;
    private readonly CatalogPageItemFavoriteCommand _catalogPageFavoriteCommand = default!;

    private CatalogInternetService? _selectedService;
    private string _searchKeyword = string.Empty;
    private IList<CatalogInternetService> _services = new List<CatalogInternetService>();
    private bool _showFavoritesOnly = default;

    public CatalogPageLoadedCommand CatalogPageLoadedCommand
        => _catalogPageLoadedCommand;

    public CatalogPageItemSelectCommand CatalogPageItemSelectCommand
        => _catalogPageItemSelectCommand;

    public AppRestartCommand AppRestartCommand
        => _appRestartCommand;

    public AboutThisAppCommand AboutThisAppCommand
        => _aboutThisAppCommand;

    public ShowDebugInfoCommand ShowDebugInfoCommand
        => _showDebugInfoCommand;

    public CatalogPageItemFavoriteCommand CatalogPageItemFavoriteCommand
        => _catalogPageFavoriteCommand;

    public CatalogInternetService? SelectedService
    {
        get => _selectedService;
        set => SetProperty(ref _selectedService, value, new string[] { nameof(SelectedService), nameof(SelectedServiceCategory), });
    }

    public CatalogInternetServiceCategory? SelectedServiceCategory
        => _selectedService?.Category;

    public string SearchKeyword
    {
        get => _searchKeyword;
        set => SetProperty(ref _searchKeyword, value);
    }

    public IList<CatalogInternetService> Services
    {
        get => _services;
        set => SetProperty(ref _services, value, new string[] { nameof(Services), nameof(HasServices), });
    }

    public bool HasServices
        => _services.Count > 0;

    public bool ShowFavoritesOnly
    {
        get => _showFavoritesOnly;
        set => SetProperty(ref _showFavoritesOnly, value);
    }
}
