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
        new(nameof(CatalogInternetService.CategoryDisplayName));

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
            view.Filter = (item) => CatalogInternetService.IsMatchedItem(item, viewModel.SearchKeyword);

            if (!view.GroupDescriptions.Contains(GroupDescription))
                view.GroupDescriptions.Add(GroupDescription);
        }
    }
}
