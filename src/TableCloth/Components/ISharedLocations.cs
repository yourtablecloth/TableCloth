namespace TableCloth.Components;

public interface ISharedLocations
{
    string AppDataDirectoryPath { get; }
    string ApplicationLogPath { get; }
    string ExecutableDirectoryPath { get; }
    string ExecutableFilePath { get; }
    string HostessZipFilePath { get; }
    string ImagesZipFilePath { get; }
    string PreferencesFilePath { get; }

    string GetImageDirectoryPath();
    string GetImageFilePath(string serviceId);
    string GetIconFilePath(string serviceId);
    string GetTempPath();
}