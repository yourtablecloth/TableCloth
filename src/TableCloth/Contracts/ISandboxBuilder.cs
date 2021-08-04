using System.Collections.Generic;
using TableCloth.Implementations.WindowsSandbox;
using TableCloth.Models.Configuration;

namespace TableCloth.Contracts
{
    public interface ISandboxBuilder
    {
        string GenerateSandboxConfiguration(string outputDirectory, TableClothConfiguration tableClothConfiguration, IList<SandboxMappedFolder> excludedDirectories);
    }
}
