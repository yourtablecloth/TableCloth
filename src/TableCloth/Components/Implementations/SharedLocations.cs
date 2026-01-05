using System;
using System.Diagnostics;
using System.IO;

namespace TableCloth.Components.Implementations;

public sealed class SharedLocations : ISharedLocations
{
    // Velopack은 LocalAppData\TableCloth에 앱을 설치하므로,
    // 설정 파일은 LocalAppData\TableCloth.Data에 저장하여 업데이트 시 삭제되지 않도록 함
    public string AppDataDirectoryPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TableCloth.Data");

    private string GetDataPath(string relativePath) =>
        Path.Combine(AppDataDirectoryPath, relativePath);

    public string ApplicationLogPath
        => GetDataPath("ApplicationLog.jsonl");

    public string PreferencesFilePath
        => GetDataPath("Preferences.json");

    public string CatalogCacheFilePath
        => GetDataPath("CatalogCache.xml");

    public string GetTempPath()
        => GetDataPath("Sandbox");

    public string GetCertificateStagingDirectoryPath()
        => Path.Combine(GetTempPath(), "assets", "certs");

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
            var mainModule = Process.GetCurrentProcess().MainModule;
            ArgumentNullException.ThrowIfNull(mainModule);

            var fileName = mainModule.FileName;
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            return fileName;
        }
    }

    public string ExecutableDirectoryPath
    {
        get
        {
            var directoryName = Path.GetDirectoryName(ExecutableFilePath);
            ArgumentNullException.ThrowIfNullOrEmpty(directoryName);
            return directoryName;
        }
    }

    public string SporkZipFilePath
        => Path.Combine(ExecutableDirectoryPath, "Spork.zip");

    public string ImagesZipFilePath
        => Path.Combine(ExecutableDirectoryPath, "Images.zip");
}
