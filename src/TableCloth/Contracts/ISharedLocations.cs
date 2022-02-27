namespace TableCloth.Contracts
{
    public interface ISharedLocations
    {
        string AppDataDirectoryPath { get; }

        string ApplicationLogPath { get; }

        string PreferencesFilePath { get; }

        string GetTempPath();

        string GetImageDirectoryPath();
    }
}
