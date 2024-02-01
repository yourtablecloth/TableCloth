using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using System;
using System.Threading.Tasks;
using TableCloth.ViewModels;

namespace TableCloth.Commands.InputPasswordWindow;

public sealed class InputPasswordWindowLoadedCommand
    : ViewModelCommandBase<InputPasswordWindowViewModel>, IAsyncCommand<InputPasswordWindowViewModel>
{
    public override void Execute(InputPasswordWindowViewModel viewModel)
        => ExecuteAsync(viewModel).SafeFireAndForget();

    public async Task ExecuteAsync(InputPasswordWindowViewModel viewModel)
        => await viewModel.NotifyViewLoadedAsync(this, EventArgs.Empty);
}
