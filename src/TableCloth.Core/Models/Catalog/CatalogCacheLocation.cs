using System;
using System.IO;

namespace TableCloth.Models.Catalog;

public static class CatalogCacheLocation
{
    public static string ForCurrentUser() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TableCloth.Data", "CatalogCache.xml");
}
