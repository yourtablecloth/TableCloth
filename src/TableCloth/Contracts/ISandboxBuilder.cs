using TableCloth.Models.Configuration;

namespace TableCloth.Contracts
{
    public interface ISandboxBuilder
    {
        string GenerateTemporaryDirectoryPath();

        string GenerateSandboxConfiguration(string outputDirectory, TableClothConfiguration tableClothConfiguration);
    }
}
