namespace TableCloth.Contracts
{
    public interface ISandboxLauncher
    {
        void RunSandbox(IAppUserInterface appUserInterface, string sandboxOutputDirectory, string wsbFilePath);

        bool IsSandboxRunning();
    }
}
