namespace Hostess.Components
{
    public interface ISharedLocations
    {
        string GetDefaultPowerShellExecutableFilePath();
        string GetDefaultX86MicrosoftEdgeExecutableFilePath();
        string GetDownloadDirectoryPath();
        string GetPicturesDirectoryPath();
        bool TryGetMicrosoftEdgeExecutableFilePath(out string msedgePath);
    }
}