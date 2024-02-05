using Hostess.ViewModels;
using Microsoft.Win32;
using System.Threading;
using System.Threading.Tasks;

namespace Hostess.Steps.Implementations
{
    public sealed class EdgeExtensionInstallStep : StepBase<EdgeExtensionInstallItemViewModel>
    {
        public override Task LoadContentForStepAsync(EdgeExtensionInstallItemViewModel viewModel, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task PlayStepAsync(EdgeExtensionInstallItemViewModel viewModel, CancellationToken cancellationToken = default)
        {
            // https://learn.microsoft.com/en-us/microsoft-edge/extensions-chromium/developer-guide/alternate-distribution-options
            using (var regKey = Registry.LocalMachine.CreateSubKey(
                @"Software\Microsoft\Edge\Extensions", true))
            {
                using (regKey.CreateSubKey(viewModel.EdgeExtensionId)) { }
                regKey.SetValue("update_url", viewModel.EdgeCrxUrl);
            }

            using (var regKey = Registry.LocalMachine.CreateSubKey(
                @"Software\Wow6432Node\Microsoft\Edge\Extensions", true))
            {
                using (regKey.CreateSubKey(viewModel.EdgeExtensionId)) { }
                regKey.SetValue("update_url", viewModel.EdgeCrxUrl);
            }

            return Task.CompletedTask;
        }

        public override bool ShouldSimulateWhenDryRun
            => true;
    }
}
