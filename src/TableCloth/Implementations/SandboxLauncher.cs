using System;
using System.Diagnostics;
using System.IO;
using TableCloth.Contracts;
using TableCloth.Resources;

namespace TableCloth.Implementations
{
    public sealed class SandboxLauncher : ISandboxLauncher
    {
        public SandboxLauncher(
            IAppMessageBox appMessageBox)
        {
            _appMessageBox = appMessageBox;
        }

        private readonly IAppMessageBox _appMessageBox;

        public void RunSandbox(IAppUserInterface appUserInteface, string sandboxOutputDirectory, string wsbFilePath)
        {
            var wsbExecPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsSandbox.exe");

            var process = new Process()
            {
                EnableRaisingEvents = true,
                StartInfo = new ProcessStartInfo(wsbExecPath, wsbFilePath) { UseShellExecute = false, },
            };

            if (!process.Start())
            {
                process.Dispose();
                _appMessageBox.DisplayError(appUserInteface, StringResources.Error_Windows_Sandbox_CanNotStart, true);
            }
        }
    }
}
