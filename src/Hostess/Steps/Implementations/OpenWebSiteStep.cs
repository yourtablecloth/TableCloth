using Hostess.Components;
using Hostess.ViewModels;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Hostess.Steps.Implementations
{
    public sealed class OpenWebSiteStep : StepBase<OpenWebSiteItemViewModel>
    {
        public override Task LoadContentForStepAsync(OpenWebSiteItemViewModel viewModel, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task PlayStepAsync(OpenWebSiteItemViewModel viewModel, CancellationToken cancellationToken = default)
        {
            Process.Start(new ProcessStartInfo(viewModel.TargetUrl)
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Maximized,
            });

            return Task.CompletedTask;
        }

        public override bool ShouldSimulateWhenDryRun
            => false;
    }
}
