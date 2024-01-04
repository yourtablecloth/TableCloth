using System;
using System.Linq;
using System.Reflection;
using System.Windows.Data;
using TableCloth.Components;
using TableCloth.Models.Catalog;
using TableCloth.ViewModels;

namespace TableCloth.Commands.CatalogPage;

public sealed class CatalogPageLoadedCommand(
    IResourceCacheManager resourceCacheManager) : ViewModelCommandBase<CatalogPageViewModel>
{
    private static readonly PropertyGroupDescription GroupDescription =
        new PropertyGroupDescription(nameof(CatalogInternetService.CategoryDisplayName));

    public override void Execute(CatalogPageViewModel viewModel)
    {
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

        viewModel.Services = services;

        var view = (CollectionView)CollectionViewSource.GetDefaultView(viewModel.Services);
        if (view != null)
        {
            view.Filter = (item) =>
            {
                var actualItem = item as CatalogInternetService;

                if (actualItem == null)
                    return false;

                var filterText = viewModel.SearchKeyword;

                if (string.IsNullOrWhiteSpace(filterText))
                    return true;

                var result = false;
                var splittedFilterText = filterText.Split(new char[] { ',', }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var eachFilterText in splittedFilterText)
                {
                    result |= actualItem.DisplayName.Contains(eachFilterText, StringComparison.OrdinalIgnoreCase)
                        || actualItem.CategoryDisplayName.Contains(eachFilterText, StringComparison.OrdinalIgnoreCase)
                        || actualItem.Url.Contains(eachFilterText, StringComparison.OrdinalIgnoreCase)
                        || actualItem.Packages.Count.ToString().Contains(eachFilterText, StringComparison.OrdinalIgnoreCase)
                        || actualItem.Packages.Any(x => x.Name.Contains(eachFilterText, StringComparison.OrdinalIgnoreCase))
                        || actualItem.Id.Contains(eachFilterText, StringComparison.OrdinalIgnoreCase);
                }

                return result;
            };

            if (!view.GroupDescriptions.Contains(GroupDescription))
                view.GroupDescriptions.Add(GroupDescription);
        }
    }
}
