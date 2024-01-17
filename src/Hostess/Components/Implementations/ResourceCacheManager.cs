using System;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace Hostess.Components.Implementations
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
                throw new Exception("Cannot load catalog document from remote source.");

            return _catalogDocument = doc.Result;
        }

        public CatalogDocument CatalogDocument
            => _catalogDocument ?? throw new InvalidOperationException(StringResources.Error_CatalogLoadFailure(null));
    }
}
