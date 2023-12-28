using System;
using System.Collections.Generic;
using TableCloth.Commands;
using TableCloth.Commands.CatalogPage;
using TableCloth.Models.Catalog;

namespace TableCloth.ViewModels;

public class CatalogPageViewModel : ViewModelBase
{
    [Obsolete("This constructor should be used only in design time context.")]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public CatalogPageViewModel() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public CatalogPageViewModel(
        CatalogPageLoadedCommand catalogPageLoadedCommand,
        CatalogPageItemSelectCommand catalogPageItemSelectCommand,
        AppRestartCommand appRestartCommand,
        AboutThisAppCommand aboutThisAppCommand)
    {
        _catalogPageLoadedCommand = catalogPageLoadedCommand;
        _catalogPageItemSelectCommand = catalogPageItemSelectCommand;
        _appRestartCommand = appRestartCommand;
        _aboutThisAppCommand = aboutThisAppCommand;
    }

    private readonly CatalogPageLoadedCommand _catalogPageLoadedCommand;
    private readonly CatalogPageItemSelectCommand _catalogPageItemSelectCommand;
    private readonly AppRestartCommand _appRestartCommand;
    private readonly AboutThisAppCommand _aboutThisAppCommand;

    private CatalogInternetService? _selectedService;
    private string _searchKeyword = string.Empty;
    private IList<CatalogInternetService> _services = new List<CatalogInternetService>();

    public CatalogPageLoadedCommand CatalogPageLoadedCommand
        => _catalogPageLoadedCommand;

    public CatalogPageItemSelectCommand CatalogPageItemSelectCommand
        => _catalogPageItemSelectCommand;

    public AppRestartCommand AppRestartCommand
        => _appRestartCommand;

    public AboutThisAppCommand AboutThisAppCommand
        => _aboutThisAppCommand;

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
}
