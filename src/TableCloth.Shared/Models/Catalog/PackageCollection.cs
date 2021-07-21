using System;
using System.Collections.ObjectModel;

namespace TableCloth.Models.Catalog
{
    [Serializable]
    public sealed class PackageCollection : KeyedCollection<string, CatalogPackageInformation>
    {
        protected override string GetKeyForItem(CatalogPackageInformation item)
            => item.Name;
    }
}
