using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using Spork.ViewModels;
using System.Threading.Tasks;
using TableCloth.Events;

namespace Spork.Commands.PrecautionsWindow
{
    public sealed class PrecautionsWindowCloseCommand : ViewModelCommandBase<PrecautionsWindowViewModel>, IAsyncCommand<PrecautionsWindowViewModel>
    {
        public override void Execute(PrecautionsWindowViewModel viewModel)
            => ExecuteAsync(viewModel).SafeFireAndForget();

        public async Task ExecuteAsync(PrecautionsWindowViewModel viewModel)
            => await viewModel.RequestCloseAsync(this, new DialogRequestEventArgs(true));
    }
}
