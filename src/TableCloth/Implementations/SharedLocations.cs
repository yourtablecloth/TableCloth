using System;
using System.IO;
using TableCloth.Contracts;

namespace TableCloth.Implementations
{
    public sealed class SharedLocations : ISharedLocations
    {
        public string AppDataDirectoryPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TableCloth");

        public string GetDataPath(string relativePath) =>
            Path.Combine(AppDataDirectoryPath, relativePath);
    }
}
