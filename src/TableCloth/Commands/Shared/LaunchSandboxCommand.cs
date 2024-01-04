using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.Shared;

public sealed class LaunchSandboxCommand : ViewModelCommandBase<ITableClothViewModel>
{
    public LaunchSandboxCommand(
        ISandboxLauncher sandboxLauncher,
        IConfigurationComposer configurationComposer)
    {
        _sandboxLauncher = sandboxLauncher;
        _configurationComposer = configurationComposer;
    }

    private readonly ISandboxLauncher _sandboxLauncher;
    private readonly IConfigurationComposer _configurationComposer;

    protected override bool EvaluateCanExecute()
        => !Helpers.GetSandboxRunningState();

    public override void Execute(ITableClothViewModel viewModel)
        => _sandboxLauncher.RunSandbox(_configurationComposer.GetConfigurationFromViewModel(viewModel));
}
