using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using System.Threading.Tasks;
using TableCloth.Events;
using TableCloth.ViewModels;

namespace TableCloth.Commands.CertSelectWindow;

public sealed class CertSelectWindowRequestCancelCommand : ViewModelCommandBase<CertSelectWindowViewModel>, IAsyncCommand<CertSelectWindowViewModel>
{
    public override void Execute(CertSelectWindowViewModel viewModel)
        => ExecuteAsync(viewModel).SafeFireAndForget();

    public async Task ExecuteAsync(CertSelectWindowViewModel viewModel)
        => await viewModel.RequestCloseAsync(this, new DialogRequestEventArgs(false));
}
