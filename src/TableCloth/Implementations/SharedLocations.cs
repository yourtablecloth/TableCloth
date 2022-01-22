using System;
using System.IO;
using TableCloth.Contracts;

namespace TableCloth.Implementations
{
    public sealed class SharedLocations : ISharedLocations
    {
        public string AppDataDirectoryPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TableCloth");

        private string GetDataPath(string relativePath) =>
            Path.Combine(AppDataDirectoryPath, relativePath);

        public string ApplicationLogPath
            => GetDataPath("ApplicationLog.jsonl");

        public string PreferencesFilePath
            => GetDataPath("Preferences.json");

        public string GetTempPath()
            => GetDataPath($"bwsb_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}");
    }
}
