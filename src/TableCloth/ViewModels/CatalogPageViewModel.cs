using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TableCloth.Commands;
using TableCloth.Commands.CatalogPage;
using TableCloth.Components;
using TableCloth.Contracts;
using TableCloth.Models.Catalog;

namespace TableCloth.ViewModels;

public class CatalogPageViewModel : ViewModelBase, IPageArgument
{
    [Obsolete("This constructor should be used only in design time context.")]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public CatalogPageViewModel() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public CatalogPageViewModel(
        CatalogCacheManager catalogCacheManager,
        CatalogPageLoadedCommand catalogPageLoadedCommand,
        CatalogPageItemSelectCommand catalogPageItemSelectCommand,
        AppRestartCommand appRestartCommand,
        AboutThisAppCommand aboutThisAppCommand)
    {
        _catalogCacheManager = catalogCacheManager;
        _catalogPageLoadedCommand = catalogPageLoadedCommand;
        _catalogPageItemSelectCommand = catalogPageItemSelectCommand;
        _appRestartCommand = appRestartCommand;
        _aboutThisAppCommand = aboutThisAppCommand;

        var catalogDoc = _catalogCacheManager.CatalogDocument;

        if (catalogDoc != null)
        {
            CatalogDocument = catalogDoc;
            Services = CatalogDocument.Services
                .OrderBy(service => typeof(CatalogInternetServiceCategory).GetField(service.Category.ToString())
                ?.GetCustomAttribute<EnumDisplayOrderAttribute>()
                ?.Order ?? 0).ToList();
        }
    }

    private readonly CatalogCacheManager _catalogCacheManager;
    private readonly CatalogPageLoadedCommand _catalogPageLoadedCommand;
    private readonly CatalogPageItemSelectCommand _catalogPageItemSelectCommand;
    private readonly AppRestartCommand _appRestartCommand;
    private readonly AboutThisAppCommand _aboutThisAppCommand;

    private CatalogDocument? _catalogDocument;
    private List<CatalogInternetService> _services = new List<CatalogInternetService>();
    private CatalogInternetService? _selectedService;
    private CatalogInternetServiceCategory? _selectedServiceCategory;
    private string _searchKeyword = string.Empty;

    public CatalogPageLoadedCommand CatalogPageLoadedCommand
        => _catalogPageLoadedCommand;

    public CatalogPageItemSelectCommand CatalogPageItemSelectCommand
        => _catalogPageItemSelectCommand;

    public AppRestartCommand AppRestartCommand
        => _appRestartCommand;

    public AboutThisAppCommand AboutThisAppCommand
        => _aboutThisAppCommand;

    public object? PageArgument { get; set; }

    public CatalogDocument? CatalogDocument
    {
        get => _catalogDocument;
        set => SetProperty(ref _catalogDocument, value);
    }

    public List<CatalogInternetService> Services
    {
        get => _services;
        set => SetProperty(ref _services, value);
    }

    public bool HasServices
        => Services != null && Services.Any();

    public CatalogInternetService? SelectedService
    {
        get => _selectedService;
        set
        {
            if (SetProperty(ref _selectedService, value) && value != null)
                SelectedServiceCategory = value.Category;
        }
    }

    public CatalogInternetServiceCategory? SelectedServiceCategory
    {
        get => _selectedServiceCategory;
        set => SetProperty(ref _selectedServiceCategory, value);
    }

    public string SearchKeyword
    {
        get => _searchKeyword;
        set => SetProperty(ref _searchKeyword, value);
    }
}
