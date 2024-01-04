using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.Shared;

public sealed class LaunchSandboxCommand(
    ISandboxLauncher sandboxLauncher,
    IConfigurationComposer configurationComposer) : ViewModelCommandBase<ITableClothViewModel>
{
    protected override bool EvaluateCanExecute()
        => !Helpers.GetSandboxRunningState();

    public override void Execute(ITableClothViewModel viewModel)
        => sandboxLauncher.RunSandbox(configurationComposer.GetConfigurationFromViewModel(viewModel));
}
