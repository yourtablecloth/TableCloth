using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.Catalog;

namespace Hostess.Components
{
    public interface IResourceCacheManager
    {
        CatalogDocument CatalogDocument { get; }

        Task<CatalogDocument> LoadCatalogDocumentAsync(CancellationToken cancellationToken = default);
    }
}