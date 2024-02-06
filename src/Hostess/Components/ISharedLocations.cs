namespace Hostess.Components
{
    public interface ISharedLocations
    {
        string GetDefaultPowerShellExecutableFilePath();
        string GetDownloadDirectoryPath();
        string GetPicturesDirectoryPath();
    }
}