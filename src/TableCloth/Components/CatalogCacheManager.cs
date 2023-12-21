using System;
using TableCloth.Models.Catalog;

namespace TableCloth.Components;

public sealed class CatalogCacheManager
{
    public CatalogCacheManager(
        CatalogDeserializer catalogDeserializer)
    {
        _catalogDeserializer = catalogDeserializer;

        _catalogDocumentFactory = new Lazy<CatalogDocument?>(
            () => _catalogDeserializer.DeserializeCatalog(),
            false);
    }

    private readonly CatalogDeserializer _catalogDeserializer;

    private Lazy<CatalogDocument?> _catalogDocumentFactory;

    public CatalogDocument? CatalogDocument => _catalogDocumentFactory.Value;
}
