namespace Spork.Components
{
    public interface ISharedLocations
    {
        string ExecutableFilePath { get; }
        string ExecutableDirectoryPath { get; }

        string GetDefaultPowerShellExecutableFilePath();
        string GetDownloadDirectoryPath();
        string GetPicturesDirectoryPath();
    }
}