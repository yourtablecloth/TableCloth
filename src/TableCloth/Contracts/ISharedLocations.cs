namespace TableCloth.Contracts
{
    public interface ISharedLocations
    {
        string AppDataDirectoryPath { get; }

        string GetDataPath(string relativePath);
    }
}
