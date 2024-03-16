using System.Threading;
using System.Threading.Tasks;
using TableCloth;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace Spork.Components.Implementations
{
    public sealed class ResourceCacheManager : IResourceCacheManager
    {
        public ResourceCacheManager(
            IResourceResolver resourceResolver)
        {
            _resourceResolver = resourceResolver;
        }

        private readonly IResourceResolver _resourceResolver;

        private CatalogDocument _catalogDocument;

        public async Task<CatalogDocument> LoadCatalogDocumentAsync(CancellationToken cancellationToken = default)
        {
            var doc = await _resourceResolver.DeserializeCatalogAsync(cancellationToken).ConfigureAwait(false);

            if (doc.Result == null)
                TableClothAppException.Throw("Cannot load catalog document from remote source.");

            return _catalogDocument = doc.Result;
        }

        public CatalogDocument CatalogDocument
            => _catalogDocument.EnsureNotNull(StringResources.Error_With_Exception(ErrorStrings.Error_CatalogLoadFailure, null));
    }
}
