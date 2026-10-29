using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TableCloth.Components;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace TableCloth.ViewModels;

public partial class CatalogWindowViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly List<CatalogInternetService> _services;
    private bool _nameOnlySearch;

    public CatalogWindowViewModel(IResourceCacheManager resourceCacheManager, INavigationService navigationService)
    {
        _navigationService = navigationService;
        _services = resourceCacheManager.CatalogDocument.Services
            .OrderBy(service => service.Category)
            .ThenBy(service => service.DisplayName, StringComparer.CurrentCulture)
            .ToList();
        RefreshResults();
    }

    public event EventHandler? CloseRequested;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private CatalogInternetService? _selectedService;

    [ObservableProperty]
    private ObservableCollection<CatalogInternetService> _filteredServices = new();

    public string ResultCountText => string.Format(UIStringResources.CatalogWindow_ResultCount, FilteredServices.Count);

    partial void OnSearchKeywordChanged(string value) => RefreshResults();

    partial void OnFilteredServicesChanged(ObservableCollection<CatalogInternetService> value)
        => OnPropertyChanged(nameof(ResultCountText));

    public void SearchByName(string query)
    {
        _nameOnlySearch = true;
        SearchKeyword = query;
        RefreshResults();
    }

    public void ShowAll()
    {
        _nameOnlySearch = false;
        SearchKeyword = string.Empty;
        RefreshResults();
    }

    private void RefreshResults()
    {
        FilteredServices = new ObservableCollection<CatalogInternetService>(
            _services.Where(service => _nameOnlySearch
                ? QuickStartInputRouter.MatchesCatalogName(service, SearchKeyword)
                : CatalogInternetService.IsMatchedItem(service, SearchKeyword)));

        if (SelectedService != null && !FilteredServices.Contains(SelectedService))
            SelectedService = null;
    }

    [RelayCommand]
    private void LaunchSelected()
    {
        if (SelectedService is not { } service)
            return;

        if (_navigationService.NavigateToQuickStartAndLaunch(new[] { service }, null))
            CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
