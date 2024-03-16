using Spork.Components;
using Spork.ViewModels;
using Microsoft.Win32;
using System;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Resources;

namespace Spork.Steps.Implementations
{
    public sealed class EnableInternetExplorerModeStep : StepBase<InstallItemViewModel>
    {
        public EnableInternetExplorerModeStep(
            ICommandLineArguments commandLineArguments)
        {
            _commandLineArguments = commandLineArguments;
        }

        private readonly ICommandLineArguments _commandLineArguments;

        public override Task LoadContentForStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override Task PlayStepAsync(InstallItemViewModel _, CancellationToken cancellationToken = default)
        {
            var parsedArgs = _commandLineArguments.Current;

            if (parsedArgs.EnableInternetExplorerMode ?? false)
            {
                using (var ieModeKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Edge", true))
                {
                    ieModeKey.SetValue("InternetExplorerIntegrationLevel", 1, RegistryValueKind.DWord);
                    ieModeKey.SetValue("InternetExplorerIntegrationSiteList", ConstantStrings.IEModePolicyXmlUrl, RegistryValueKind.String);
                }

                using (var ieModeKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Wow6432Node\Microsoft\Edge", true))
                {
                    ieModeKey.SetValue("InternetExplorerIntegrationLevel", 1, RegistryValueKind.DWord);
                    ieModeKey.SetValue("InternetExplorerIntegrationSiteList", ConstantStrings.IEModePolicyXmlUrl, RegistryValueKind.String);
                }
            }

            return Task.CompletedTask;
        }

        public override bool ShouldSimulateWhenDryRun
            => true;
    }
}
