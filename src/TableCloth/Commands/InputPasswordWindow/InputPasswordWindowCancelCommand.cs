using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using System.Threading.Tasks;
using TableCloth.Events;
using TableCloth.ViewModels;

namespace TableCloth.Commands.InputPasswordWindow;

public sealed class InputPasswordWindowCancelCommand : ViewModelCommandBase<InputPasswordWindowViewModel>, IAsyncCommand<InputPasswordWindowViewModel>
{
    public override void Execute(InputPasswordWindowViewModel viewModel)
        => ExecuteAsync(viewModel).SafeFireAndForget();

    public async Task ExecuteAsync(InputPasswordWindowViewModel viewModel)
    {
        viewModel.ValidatedCertPair = null;
        await viewModel.RequestCloseAsync(this, new DialogRequestEventArgs(false));
    }
}
