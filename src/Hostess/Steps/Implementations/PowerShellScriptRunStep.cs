using Hostess.Components;
using Hostess.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TableCloth;
using TableCloth.Resources;

namespace Hostess.Steps.Implementations
{
    public sealed class PowerShellScriptRunStep : StepBase<PowerShellScriptInstallItemViewModel>
    {
        public PowerShellScriptRunStep(
            ISharedLocations sharedLocations)
        {
            _sharedLocations = sharedLocations;
        }

        private readonly ISharedLocations _sharedLocations;

        public override async Task LoadContentForStepAsync(PowerShellScriptInstallItemViewModel viewModel, CancellationToken cancellationToken = default)
        {
            var downloadFolderPath = _sharedLocations.GetDownloadDirectoryPath();
            var tempFileName = $"bootstrap_{Guid.NewGuid():n}.ps1";
            var tempFilePath = Path.Combine(downloadFolderPath, tempFileName);
            viewModel.DownloadedScriptFilePath = tempFilePath;

            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);

            using (var stream = File.OpenWrite(tempFilePath))
            {
                using (var streamWriter = new StreamWriter(stream, Encoding.Unicode))
                {
                    await streamWriter.WriteAsync(viewModel.ScriptContent).ConfigureAwait(false);
                }
            }
        }

        public override async Task PlayStepAsync(PowerShellScriptInstallItemViewModel viewModel, CancellationToken cancellationToken = default)
        {
            var powershellPath = _sharedLocations.GetDefaultPowerShellExecutableFilePath();

            if (!File.Exists(powershellPath))
                TableClothAppException.Throw(ErrorStrings.Error_No_WindowsPowerShell);

            var tempFilePath = viewModel.DownloadedScriptFilePath;

            var psi = new ProcessStartInfo(powershellPath, $"Set-ExecutionPolicy Bypass -Scope Process -Force; {tempFilePath}")
            {
                UseShellExecute = false,
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

        public override bool ShouldSimulateWhenDryRun
            => true;
    }
}
