using System;
using System.Diagnostics;
using System.IO;

namespace TableCloth.Components.Implementations;

public sealed class SharedLocations : ISharedLocations
{
    // Velopack�� LocalAppData\TableCloth�� ���� ��ġ�ϹǷ�,
    // ���� ������ LocalAppData\TableCloth.Data�� �����Ͽ� ������Ʈ �� �������� �ʵ��� ��
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

    public string ImagesZipFilePath
        => Path.Combine(ExecutableDirectoryPath, "Images.zip");

    public string DefaultDataDirectoryPath
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "TableCloth",
            "Data");

    public string GetSandboxAppStagingPath(string sandboxStagingDirectory)
        => Path.Combine(sandboxStagingDirectory, "App");

    public string GetEffectiveDataDirectoryPath(string? configuredPath)
        => string.IsNullOrWhiteSpace(configuredPath)
            ? DefaultDataDirectoryPath
            : configuredPath!;

    public bool IsMountableDataDirectory(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return true;

            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root))
                return true;

            if (root.StartsWith(@"\\", StringComparison.Ordinal))
                return false; // UNC 경로는 마운트 불가

            // 로컬 고정 디스크만 마운트 가능. 네트워크/이동식/미확정 드라이브는 마운트 불가로 본다.
            return new DriveInfo(root).DriveType is DriveType.Fixed;
        }
        catch
        {
            // 판별 실패 시에는 거짓 차단/경고를 피하기 위해 마운트 가능으로 간주한다.
            return true;
        }
    }
}
