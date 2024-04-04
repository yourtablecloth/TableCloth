using Spork.Components;
using Spork.ViewModels;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TableCloth;
using TableCloth.Resources;

namespace Spork.Steps.Implementations
{
    public sealed class VerifyWindowsContainerEnvironmentStep : StepBase<InstallItemViewModel>
    {
        public VerifyWindowsContainerEnvironmentStep(
            IAppMessageBox appMessageBox)
        {
            _appMessageBox = appMessageBox;
        }

        private readonly IAppMessageBox _appMessageBox;

        public override Task<bool> EvaluateRequiredStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public override Task LoadContentForStepAsync(InstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task PlayStepAsync(InstallItemViewModel _, Action<double> progressCallback, CancellationToken cancellationToken = default)
        {
            if (!Helpers.SandboxAccountNames.Contains(Environment.UserName, StringComparer.Ordinal))
            {
                var response = _appMessageBox.DisplayQuestion(
                    AskStrings.Ask_WarningForNonSandboxEnvironment,
                    defaultAnswer: MessageBoxResult.No);

                if (response != MessageBoxResult.Yes)
                    Environment.Exit(1);
            }

            return Task.CompletedTask;
        }

        public override bool ShouldSimulateWhenDryRun
            => true;
    }
}
