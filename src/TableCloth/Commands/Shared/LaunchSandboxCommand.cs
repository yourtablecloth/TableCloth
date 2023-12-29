using System;
using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.Shared;

public sealed class LaunchSandboxCommand : ViewModelCommandBase<ITableClothViewModel>
{
    public LaunchSandboxCommand(
        SandboxLauncher sandboxLauncher,
        ConfigurationComposer configurationComposer)
    {
        _sandboxLauncher = sandboxLauncher;
        _configurationComposer = configurationComposer;
    }

    private readonly SandboxLauncher _sandboxLauncher;
    private readonly ConfigurationComposer _configurationComposer;

    protected override bool EvaluateCanExecute()
        => !Helpers.GetSandboxRunningState();

    public override void Execute(ITableClothViewModel viewModel)
        => _sandboxLauncher.RunSandbox(_configurationComposer.GetConfigurationFromViewModel(viewModel));
}
