using System;
using System.Collections.ObjectModel;
using TableCloth.Models.TableClothCatalog;

namespace TableCloth.Internals
{
    [Serializable]
    public sealed class PackageCollection : KeyedCollection<string, CatalogPackageInformation>
    {
        protected override string GetKeyForItem(CatalogPackageInformation item)
            => item.Name;
    }
}
