using Spork.Components;
using Spork.ViewModels;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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

        private static readonly string[] ValidAccountNames = new string[]
        {
            "WDAGUtilityAccount",
        };

        public override Task LoadContentForStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task PlayStepAsync(InstallItemViewModel _, CancellationToken cancellationToken = default)
        {
            if (!ValidAccountNames.Contains(Environment.UserName, StringComparer.Ordinal))
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
