using System;
using System.IO;
using TableCloth.Contracts;

namespace TableCloth.Implementations
{
    public sealed class SharedLocations : ISharedLocations
    {
        public string AppDataDirectoryPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "TableCloth");

        public string GetDataPath(string relativePath) =>
            Path.Combine(AppDataDirectoryPath, relativePath);
    }
}
