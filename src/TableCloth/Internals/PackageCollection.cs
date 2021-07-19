using System;
using System.Collections.ObjectModel;
using TableCloth.Models;

namespace TableCloth.Internals
{
    [Serializable]
    public sealed class PackageCollection : KeyedCollection<string, PackageInformation>
    {
        protected override string GetKeyForItem(PackageInformation item)
            => item.Name;
    }
}
