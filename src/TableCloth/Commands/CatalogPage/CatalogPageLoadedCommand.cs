using System;
using System.Linq;
using System.Reflection;
using System.Windows.Data;
using TableCloth.Components;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.ViewModels;

namespace TableCloth.Commands.CatalogPage;

public sealed class CatalogPageLoadedCommand : CommandBase
{
    public CatalogPageLoadedCommand(
        ResourceCacheManager resourceCacheManager)
    {
        _resourceCacheManager = resourceCacheManager;
    }

    private readonly ResourceCacheManager _resourceCacheManager;

    private static readonly PropertyGroupDescription GroupDescription =
        new PropertyGroupDescription(nameof(CatalogInternetService.CategoryDisplayName));

    public override void Execute(object? parameter)
    {
        if (parameter is not CatalogPageViewModel viewModel)
            throw new ArgumentException(nameof(parameter));

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
