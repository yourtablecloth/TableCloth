using System;
using TableCloth.Models.Catalog;

namespace TableCloth.Contracts
{
    public interface ICatalogDeserializer
    {
        CatalogDocument DeserializeCatalog(Uri targetUri);
    }
}
