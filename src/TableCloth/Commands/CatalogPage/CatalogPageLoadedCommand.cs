using AsyncAwaitBestPractices;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Data;
using TableCloth.Components;
using TableCloth.Models.Catalog;
using TableCloth.ViewModels;

namespace TableCloth.Commands.CatalogPage;

public sealed class CatalogPageLoadedCommand(
    IPreferencesManager preferencesManager,
    IResourceCacheManager resourceCacheManager) : ViewModelCommandBase<CatalogPageViewModel>
{
    private static readonly PropertyGroupDescription GroupDescription =
        new(nameof(CatalogInternetService.CategoryDisplayName));

    public override void Execute(CatalogPageViewModel viewModel)
        => ExecuteAsync(viewModel).SafeFireAndForget();

    public async Task ExecuteAsync(CatalogPageViewModel viewModel)
    {
        var currentConfig = await preferencesManager.LoadPreferencesAsync();
        currentConfig ??= preferencesManager.GetDefaultPreferences();

        var doc = resourceCacheManager.CatalogDocument;
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

        viewModel.ShowFavoritesOnly = currentConfig.ShowFavoritesOnly;
        viewModel.Services = services;

        foreach (var eachFavoriteServce in services)
            eachFavoriteServce.IsFavorite = currentConfig.Favorites.Contains(eachFavoriteServce.Id, StringComparer.OrdinalIgnoreCase);

        viewModel.PropertyChanged += ViewModel_PropertyChanged;

        var view = (CollectionView)CollectionViewSource.GetDefaultView(viewModel.Services);
        if (view != null)
        {
            view.Filter = (item) => CatalogInternetService.IsMatchedItem(item, viewModel.SearchKeyword, viewModel.ShowFavoritesOnly);

            if (!view.GroupDescriptions.Contains(GroupDescription))
                view.GroupDescriptions.Add(GroupDescription);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => OnViewModelPropertyChangedAsync(sender, e).SafeFireAndForget();

    private async Task OnViewModelPropertyChangedAsync(object? sender, PropertyChangedEventArgs e)
    {
        var viewModel = sender.EnsureArgumentNotNullWithCast<object, CatalogPageViewModel>(
            "Selected parameter is not a supported type.", nameof(sender));

        var currentConfig = await preferencesManager.LoadPreferencesAsync();
        currentConfig ??= preferencesManager.GetDefaultPreferences();

        switch (e.PropertyName)
        {
            case nameof(CatalogPageViewModel.ShowFavoritesOnly):
                currentConfig.ShowFavoritesOnly = viewModel.ShowFavoritesOnly;
                break;

            default:
                return;
        }

        await preferencesManager.SavePreferencesAsync(currentConfig);
    }
}
