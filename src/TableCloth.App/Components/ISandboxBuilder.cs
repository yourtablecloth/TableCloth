using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.Configuration;
using TableCloth.Models.WindowsSandbox;

namespace TableCloth.Components;

public interface ISandboxBuilder
{
    Task<string?> GenerateSandboxConfigurationAsync(
        string outputDirectory,
        TableClothConfiguration tableClothConfiguration,
        IList<SandboxMappedFolder> excludedDirectories,
        CancellationToken cancellationToken = default);
}