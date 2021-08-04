using System.Diagnostics;

namespace TableCloth.Contracts
{
    public interface ISandboxLauncher
    {
        Process RunSandbox(IAppUserInterface appUserInterface, string sandboxOutputDirectory, string wsbFilePath, bool cleanup);
    }
}
