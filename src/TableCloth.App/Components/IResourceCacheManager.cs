using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using TableCloth.Models.Catalog;

namespace TableCloth.Components;

public interface IResourceCacheManager
{
    CatalogDocument CatalogDocument { get; }

    ImageSource? GetImage(string siteId);
    Task<CatalogDocument> LoadCatalogDocumentAsync(CancellationToken cancellationToken = default);
    Task LoadSiteImagesAsync(CancellationToken cancellationToken = default);
}