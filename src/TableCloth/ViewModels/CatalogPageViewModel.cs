using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Data;
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

public partial class CatalogPageViewModel : ViewModelBase
{
    protected CatalogPageViewModel() { }

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

    private static readonly PropertyGroupDescription GroupDescription =
        new(nameof(CatalogInternetService.CategoryDisplayName));

    [RelayCommand]
    private async Task CatalogPageLoaded()
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

        var view = (CollectionView)CollectionViewSource.GetDefaultView(Services);
        if (view != null)
        {
            view.Filter = (item) => CatalogInternetService.IsMatchedItem(item, SearchKeyword, ShowFavoritesOnly);

            if (!view.GroupDescriptions.Contains(GroupDescription))
                view.GroupDescriptions.Add(GroupDescription);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => OnViewModelPropertyChangedAsync(sender, e).SafeFireAndForget();

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
        aboutWindow.ShowDialog();
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

    [RelayCommand]
    private async Task CatalogPageFavorite()
    {
        if (SelectedService == null)
            return;

        var settings = await _preferencesManager.LoadPreferencesAsync();
        settings!.Favorites ??= new List<string>();
        if (SelectedService.IsFavorite)
            settings.Favorites.Add(SelectedService.Id);
        else if (settings.Favorites.Contains(SelectedService.Id))
            settings.Favorites.Remove(SelectedService.Id);

        await _preferencesManager.SavePreferencesAsync(settings);
    }
}
