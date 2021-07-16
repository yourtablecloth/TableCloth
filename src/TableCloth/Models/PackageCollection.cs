using System;
using System.Collections.ObjectModel;

namespace TableCloth.Models
{
    [Serializable]
    public sealed class PackageCollection : KeyedCollection<string, PackageInformation>
    {
        protected override string GetKeyForItem(PackageInformation item)
            => item.Name;
    }
}
