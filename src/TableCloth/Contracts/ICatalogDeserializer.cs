using System;
using TableCloth.Models.Catalog;

namespace TableCloth.Contracts
{
    public interface ICatalogDeserializer
    {
        DateTimeOffset? CatalogLastModified { get; }

        CatalogDocument DeserializeCatalog();

        DateTimeOffset? IEModeListLastModified { get; }

        IEModeListDocument DeserializeIEModeList();
    }
}
