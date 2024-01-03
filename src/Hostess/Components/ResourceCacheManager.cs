using System;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace Hostess.Components
{
    public sealed class ResourceCacheManager
    {
        public ResourceCacheManager(
            ResourceResolver resourceResolver)
        {
            _resourceResolver = resourceResolver;
        }

        private readonly ResourceResolver _resourceResolver;

        private CatalogDocument _catalogDocument;

        public async Task<CatalogDocument> LoadCatalogDocumentAsync(CancellationToken cancellationToken = default)
            => _catalogDocument = await _resourceResolver.DeserializeCatalogAsync(cancellationToken).ConfigureAwait(false);

        public CatalogDocument CatalogDocument
            => _catalogDocument ?? throw new InvalidOperationException(StringResources.HostessError_CatalogLoadFailure(null));
    }
}
