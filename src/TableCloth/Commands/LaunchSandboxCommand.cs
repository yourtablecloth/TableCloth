using System;
using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands;

public sealed class LaunchSandboxCommand : CommandBase
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
        => (!Helpers.GetSandboxRunningState());

    public override void Execute(object? parameter)
    {
        if (parameter is not ITableClothViewModel viewModel)
            throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

        var config = _configurationComposer.GetConfigurationFromViewModel(viewModel);
        _sandboxLauncher.RunSandbox(config);
    }
}
