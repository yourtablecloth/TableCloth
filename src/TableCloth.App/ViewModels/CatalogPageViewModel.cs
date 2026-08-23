using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading.Tasks;
using TableCloth.Components;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public partial class CatalogPageViewModelForDesigner : CatalogPageViewModel
{
    public IList<CatalogInternetService> ServicesForDesigner
        => DesignTimeResources.DesignTimeCatalogDocument.Services;
}

public partial class CatalogPageViewModel : ObservableObject
{
    protected CatalogPageViewModel() { }

    [ActivatorUtilitiesConstructor]
    public CatalogPageViewModel(
        IPreferencesManager preferencesManager,
        IResourceCacheManager resourceCacheManager,
        INavigationService navigationService,
        IAppRestartManager appRestartManager,
        IAppUserInterface appUserInterface,
        ICommandLineArguments commandLineArguments,
        IAppMessageBox appMessageBox)
    {
        _preferencesManager = preferencesManager;
        _resourceCacheManager = resourceCacheManager;
        _navigationService = navigationService;
        _appRestartManager = appRestartManager;
        _appUserInterface = appUserInterface;
        _commandLineArguments = commandLineArguments;
        _appMessageBox = appMessageBox;
    }

    private readonly IPreferencesManager _preferencesManager = default!;
    private readonly IResourceCacheManager _resourceCacheManager = default!;
    private readonly INavigationService _navigationService = default!;
    private readonly IAppRestartManager _appRestartManager = default!;
    private readonly IAppUserInterface _appUserInterface = default!;
    private readonly ICommandLineArguments _commandLineArguments = default!;
    private readonly IAppMessageBox _appMessageBox = default!;

    // 이슈 #296: WPF CollectionViewSource/PropertyGroupDescription → VM측 계산 그룹 컬렉션(RebuildView).

    [RelayCommand]
    private async Task CatalogPageLoaded()
    {
        IsLoading = true;
        HasCatalogLoadFailed = false;

        try
        {
            var currentConfig = await _preferencesManager.LoadPreferencesAsync();
            currentConfig ??= _preferencesManager.GetDefaultPreferences();

            var doc = _resourceCacheManager.CatalogDocument;
            var services = doc.Services.OrderBy(service =>
            {
                var fieldInfo = typeof(CatalogInternetServiceCategory).GetField(service.Category.ToString());

                if (fieldInfo == null)
                    return default;

                var customAttribute = fieldInfo.GetCustomAttribute<EnumDisplayOrderAttribute>();

                if (customAttribute == null)
                    return default;

                return customAttribute.Order;
            }).ToList();

            ShowFavoritesOnly = currentConfig.ShowFavoritesOnly;
            Services = services;

            foreach (var eachFavoriteServce in services)
                eachFavoriteServce.IsFavorite = currentConfig.Favorites.Contains(eachFavoriteServce.Id, StringComparer.OrdinalIgnoreCase);

            PropertyChanged += ViewModel_PropertyChanged;

            RebuildView();

            if (!HasServices)
                HasCatalogLoadFailed = true;
        }
        catch
        {
            HasCatalogLoadFailed = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 이슈 #296: WPF 코드비하인드의 CollectionViewSource.Refresh 대체 — 검색어/즐겨찾기 변경 시 그룹 재구성.
        if (e.PropertyName is nameof(SearchKeyword) or nameof(ShowFavoritesOnly))
            RebuildView();

        OnViewModelPropertyChangedAsync(sender, e).SafeFireAndForget();
    }

    /// <summary>
    /// 이슈 #296: 현재 검색어/즐겨찾기 필터를 적용해 서비스를 카테고리별로 그룹화한다(CollectionViewSource 대체).
    /// Services 가 카테고리 표시 순서로 정렬돼 있어 GroupBy 가 순서를 보존한다.
    /// </summary>
    private void RebuildView()
    {
        var groups = (Services ?? new List<CatalogInternetService>())
            .Where(x => CatalogInternetService.IsMatchedItem(x, SearchKeyword, ShowFavoritesOnly))
            .GroupBy(x => x.CategoryDisplayName)
            .Select(g => new CatalogServiceGroup(g.Key, g.ToList()))
            .ToList();
        ServiceGroups = new ObservableCollection<CatalogServiceGroup>(groups);
    }

    private async Task OnViewModelPropertyChangedAsync(object? sender, PropertyChangedEventArgs e)
    {
        var viewModel = sender as CatalogPageViewModel;
        ArgumentNullException.ThrowIfNull(viewModel);

        var currentConfig = await _preferencesManager.LoadPreferencesAsync();
        currentConfig ??= _preferencesManager.GetDefaultPreferences();

        switch (e.PropertyName)
        {
            case nameof(CatalogPageViewModel.ShowFavoritesOnly):
                currentConfig.ShowFavoritesOnly = viewModel.ShowFavoritesOnly;
                break;

            default:
                return;
        }

        await _preferencesManager.SavePreferencesAsync(currentConfig);
    }

    [RelayCommand]
    private void CatalogPageItemSelect()
    {
        if (SelectedService == null)
            return;

        _navigationService.NavigateToDetail(SearchKeyword, SelectedService, null);
    }

    [RelayCommand]
    private void AboutThisApp()
    {
        var aboutWindow = _appUserInterface.CreateAboutWindow();
        _appUserInterface.ShowDialog(aboutWindow);
    }

    [RelayCommand]
    private void ShowDebugInfo()
    {
        _appMessageBox.DisplayInfo(StringResources.TableCloth_DebugInformation(
            Process.GetCurrentProcess().ProcessName,
            string.Join(" ", _commandLineArguments.GetCurrent().RawArguments),
            _commandLineArguments.GetCurrent().ToString())
        );
    }

    [RelayCommand]
    private void AppRestart()
    {
        _appRestartManager.RestartNow();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedServiceCategory))]
    private CatalogInternetService? _selectedService;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasServices))]
    private IList<CatalogInternetService> _services = new List<CatalogInternetService>();

    // 이슈 #296: 뷰가 바인딩하는 파생 그룹 컬렉션. RebuildView()가 필터/그룹을 적용해 채운다.
    [ObservableProperty]
    private ObservableCollection<CatalogServiceGroup> _serviceGroups = new();

    [ObservableProperty]
    private bool _showFavoritesOnly = default;

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private bool _hasCatalogLoadFailed = false;

    public CatalogInternetServiceCategory? SelectedServiceCategory
        => SelectedService?.Category;

    public bool HasServices
        => Services.Count > 0;

    [RelayCommand]
    private async Task CatalogPageItemFavorite(CatalogInternetService? service)
    {
        if (service == null)
            return;

        var settings = await _preferencesManager.LoadPreferencesAsync();
        settings!.Favorites ??= new List<string>();
        if (service.IsFavorite)
            settings.Favorites.Add(service.Id);
        else if (settings.Favorites.Contains(service.Id))
            settings.Favorites.Remove(service.Id);

        await _preferencesManager.SavePreferencesAsync(settings);
    }
}

/// <summary>
/// 이슈 #296: WPF CollectionView 그룹(카테고리) 대체. 카탈로그 사이트를 카테고리별로 묶은 뷰 그룹.
/// </summary>
public sealed class CatalogServiceGroup(string name, IReadOnlyList<CatalogInternetService> items)
{
    public string Name { get; } = name;

    public IReadOnlyList<CatalogInternetService> Items { get; } = items ?? Array.Empty<CatalogInternetService>();

    public int ItemCount => Items.Count;
}
