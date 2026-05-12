using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.Configuration;

namespace TableCloth.Components;

public interface ISandboxLauncher
{
    Task RunSandboxAsync(TableClothConfiguration config, CancellationToken cancellationToken = default);
    bool ValidateSandboxSpecFile(string wsbFilePath, out string? reason);
}