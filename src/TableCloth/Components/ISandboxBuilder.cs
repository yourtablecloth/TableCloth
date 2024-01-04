using System.Collections.Generic;
using TableCloth.Models.Configuration;
using TableCloth.Models.WindowsSandbox;

namespace TableCloth.Components;

public interface ISandboxBuilder
{
    string GenerateSandboxConfiguration(string outputDirectory, TableClothConfiguration tableClothConfiguration, IList<SandboxMappedFolder> excludedDirectories);
}