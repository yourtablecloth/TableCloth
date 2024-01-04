using TableCloth.Models.Configuration;

namespace TableCloth.Components;

public interface ISandboxLauncher
{
    void RunSandbox(TableClothConfiguration config);
    bool ValidateSandboxSpecFile(string wsbFilePath, out string? reason);
}