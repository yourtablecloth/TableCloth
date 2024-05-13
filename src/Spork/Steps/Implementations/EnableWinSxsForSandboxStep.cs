using Spork.Components;
using Spork.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TableCloth;
using TableCloth.Resources;

namespace Spork.Steps.Implementations
{
    public sealed class EnableWinSxsForSandboxStep : StepBase<InstallItemViewModel>
    {
        public EnableWinSxsForSandboxStep(ISharedLocations sharedLocations)
        {
            _sharedLocations = sharedLocations;
        }

        private readonly ISharedLocations _sharedLocations;

        public override bool ShouldSimulateWhenDryRun
            => true;

        public override Task<bool> EvaluateRequiredStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken = default)
            => Task.FromResult(Helpers.IsUnderWindowsSandboxSession());

        public override async Task LoadContentForStepAsync(InstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default)
        {
            var downloadFolderPath = _sharedLocations.GetDownloadDirectoryPath();
            var enableWinSxSScriptPath = Path.Combine(downloadFolderPath, "enable_winsxs.ps1");

            if (File.Exists(enableWinSxSScriptPath))
                File.Delete(enableWinSxSScriptPath);

            using (var stream = File.OpenWrite(enableWinSxSScriptPath))
            {
                using (var streamWriter = new StreamWriter(stream, Encoding.Unicode))
                {
                    await streamWriter.WriteAsync(@"takeown.exe /f ""$env:WINDIR\winsxs"" /a /r /d Y
icacls.exe ""$env:WINDIR\winsxs"" /grant ""Administrators:(OI)(CI)F"" /T
mkdir ""$env:WINDIR\winsxs\Backup"" | Out-Null
mkdir ""$env:WINDIR\winsxs\Catalogs"" | Out-Null
mkdir ""$env:WINDIR\winsxs\FileMaps"" | Out-Null
mkdir ""$env:WINDIR\winsxs\Fusion"" | Out-Null
mkdir ""$env:WINDIR\winsxs\InstallTemp"" | Out-Null
Set-Service -StartupType Automatic -ServiceName TrustedInstaller
Restart-Service -ServiceName TrustedInstaller
").ConfigureAwait(false);
                }
            }
        }

        public override async Task PlayStepAsync(InstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default)
        {
            var powershellPath = _sharedLocations.GetDefaultPowerShellExecutableFilePath();

            if (!File.Exists(powershellPath))
                TableClothAppException.Throw(ErrorStrings.Error_No_WindowsPowerShell);

            var downloadFolderPath = _sharedLocations.GetDownloadDirectoryPath();
            var enableWinSxSScriptPath = Path.Combine(downloadFolderPath, "enable_winsxs.ps1");

            var psi = new ProcessStartInfo(powershellPath, $"Set-ExecutionPolicy Bypass -Scope Process -Force; {enableWinSxSScriptPath}")
            {
                UseShellExecute = false,
                CreateNoWindow = !Helpers.IsDevelopmentBuild,
            };

            var cpSource = new TaskCompletionSource<int>();
            using (var process = new Process() { StartInfo = psi, })
            {
                process.EnableRaisingEvents = true;
                process.Exited += (_sender, _e) =>
                {
                    var realSender = _sender as Process;
                    cpSource.SetResult(realSender.ExitCode);
                };

                if (!process.Start())
                    TableClothAppException.Throw(ErrorStrings.Error_Package_CanNotStart);

                await cpSource.Task.ConfigureAwait(false);
            }
        }
    }
}
