using System;
using System.Diagnostics;
using System.IO;

namespace TableCloth.Components
{
    public sealed class SharedLocations
    {
        public string AppDataDirectoryPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TableCloth");

        private string GetDataPath(string relativePath) =>
            Path.Combine(AppDataDirectoryPath, relativePath);

        public string ApplicationLogPath
            => GetDataPath("ApplicationLog.jsonl");

        public string PreferencesFilePath
            => GetDataPath("Preferences.json");

        public string GetTempPath()
            => GetDataPath($"bwsb_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}");

        public string GetImageDirectoryPath()
            => GetDataPath("images");

        public string ExecutableFilePath
        {
            get
            {
                var mainModule = Process.GetCurrentProcess().MainModule
                    ?? throw new Exception("Cannot obtain process main module information.");
                return mainModule.FileName
                    ?? throw new Exception("Cannot obtain executable file name.");
            }
        }

        public string ExecutableDirectoryPath
        {
            get
            {
                return Path.GetDirectoryName(ExecutableFilePath)
                    ?? throw new Exception("Cannot obtain executable directory path.");
            }
        }

        public string HostessZipFilePath
            => Path.Combine(ExecutableDirectoryPath, "Hostess.zip");

        public string ImagesZipFilePath
            => Path.Combine(ExecutableDirectoryPath, "Images.zip");
    }
}
