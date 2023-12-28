using System;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace TableCloth.Components;

public sealed class CatalogCacheManager
{
    public CatalogCacheManager(
        CatalogDeserializer catalogDeserializer)
    {
        _catalogDeserializer = catalogDeserializer;
    }

    private readonly CatalogDeserializer _catalogDeserializer;

    private CatalogDocument? _catalogDocument;

    public async Task<CatalogDocument> LoadCatalogDocumentAsync(CancellationToken cancellationToken = default)
        => _catalogDocument = await _catalogDeserializer.DeserializeCatalogAsync(cancellationToken).ConfigureAwait(false);

    public CatalogDocument CatalogDocument
        => _catalogDocument ?? throw new InvalidOperationException(StringResources.HostessError_CatalogLoadFailure(null));
}
