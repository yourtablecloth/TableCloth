using Avalonia.Media;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.Catalog;

namespace TableCloth.Components;

public interface IResourceCacheManager
{
    CatalogDocument CatalogDocument { get; }

    // 이슈 #296: WPF ImageSource → Avalonia IImage.
    IImage? GetImage(string siteId);
    Task<CatalogDocument> LoadCatalogDocumentAsync(CancellationToken cancellationToken = default);
    Task LoadSiteImagesAsync(CancellationToken cancellationToken = default);
}