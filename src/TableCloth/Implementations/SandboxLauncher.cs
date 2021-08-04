using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
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

        public Process RunSandbox(IAppUserInterface appUserInteface, string sandboxOutputDirectory, string wsbFilePath, bool cleanup)
        {
            var wsbExecPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsSandbox.exe");

            var process = new Process()
            {
                EnableRaisingEvents = true,
                StartInfo = new ProcessStartInfo(wsbExecPath, wsbFilePath) { UseShellExecute = false, },
            };

            if (cleanup)
            {
                process.Exited += (__sender, __e) =>
                {
                    try
                    {
                        if (__sender is Process realSender)
                        {
                            var exitCode = realSender.ExitCode;

                            if (exitCode != 0x0 && exitCode != unchecked((int)0x800700b7u))
                                _appMessageBox.DisplayError(appUserInteface, StringResources.Error_Sandbox_ErrorCode_NonZero(exitCode), false);
                        }

                        for (var i = 0; i < 5; i++)
                        {
                            try
                            {
                                if (Directory.Exists(sandboxOutputDirectory))
                                    Directory.Delete(sandboxOutputDirectory, true);
                                else
                                    break;
                            }
                            catch { Thread.Sleep(TimeSpan.FromSeconds(0.5d)); }
                        }
                    }
                    catch (Exception ex)
                    {
                        _appMessageBox.DisplayError(appUserInteface, StringResources.Error_Cannot_Remove_TempDirectory(ex), false);
                        TryOpenWindowsExplorer(appUserInteface, sandboxOutputDirectory);
                    }
                };
            }

            if (process.Start())
                return process;

            process.Dispose();
            _appMessageBox.DisplayError(appUserInteface, StringResources.Error_Windows_Sandbox_CanNotStart, true);
            return null;
        }

        private void TryOpenWindowsExplorer(IAppUserInterface appUserInteface, string targetPath)
        {
            var explorerFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "explorer.exe");
            if (!File.Exists(explorerFilePath))
            {
                _appMessageBox.DisplayError(appUserInteface, StringResources.Error_Windows_Explorer_Missing, false);
                return;
            }

            using var explorerProcess = new Process()
            {
                StartInfo = new ProcessStartInfo(explorerFilePath, targetPath),
            };

            if (!explorerProcess.Start())
            {
                _appMessageBox.DisplayError(appUserInteface, StringResources.Error_Windows_Explorer_CanNotStart, false);
                return;
            }
        }
    }
}
