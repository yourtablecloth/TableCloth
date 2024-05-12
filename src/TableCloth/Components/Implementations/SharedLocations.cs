using System;
using System.Diagnostics;
using System.IO;

namespace TableCloth.Components.Implementations;

public sealed class SharedLocations : ISharedLocations
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

    public string GetImageFilePath(string serviceId)
        => Path.Combine(GetImageDirectoryPath(), $"{serviceId}.png");

    public string GetIconFilePath(string serviceId)
        => Path.Combine(GetImageDirectoryPath(), $"{serviceId}.ico");

    public string ExecutableFilePath
    {
        get
        {
            var mainModule = Process.GetCurrentProcess().MainModule
                .EnsureNotNull("Cannot obtain process main module information.");
            return mainModule.FileName
                .EnsureNotNull("Cannot obtain executable file name.");
        }
    }

    public string ExecutableDirectoryPath
        => Path.GetDirectoryName(ExecutableFilePath).EnsureNotNull("Cannot obtain executable directory path.");

    public string SporkZipFilePath
        => Path.Combine(ExecutableDirectoryPath, "Spork.zip");

    public string SpongeZipFilePath
        => Path.Combine(ExecutableDirectoryPath, "Sponge.zip");

    public string ImagesZipFilePath
        => Path.Combine(ExecutableDirectoryPath, "Images.zip");
}
