using CommunityToolkit.Mvvm.ComponentModel;
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

    [ObservableProperty]
    private CatalogPageLoadedCommand _catalogPageLoadedCommand = default!;

    [ObservableProperty]
    private CatalogPageItemSelectCommand _catalogPageItemSelectCommand = default!;

    [ObservableProperty]
    private AppRestartCommand _appRestartCommand = default!;

    [ObservableProperty]
    private AboutThisAppCommand _aboutThisAppCommand = default!;

    [ObservableProperty]
    private ShowDebugInfoCommand _showDebugInfoCommand = default!;

    [ObservableProperty]
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
