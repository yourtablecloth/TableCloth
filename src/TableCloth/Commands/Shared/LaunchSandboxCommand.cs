using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using System.Threading.Tasks;
using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.Shared;

public sealed class LaunchSandboxCommand(
    ISandboxLauncher sandboxLauncher,
    IConfigurationComposer configurationComposer) : ViewModelCommandBase<ITableClothViewModel>, IAsyncCommand<ITableClothViewModel>
{
    protected override bool EvaluateCanExecute()
        => !Helpers.IsWindowsSandboxRunning();

    public override void Execute(ITableClothViewModel viewModel)
        => ExecuteAsync(viewModel).SafeFireAndForget();

    public async Task ExecuteAsync(ITableClothViewModel viewModel)
        => await sandboxLauncher.RunSandboxAsync(configurationComposer.GetConfigurationFromViewModel(viewModel));
}
