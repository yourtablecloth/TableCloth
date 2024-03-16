using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using Spork.ViewModels;
using System.Threading.Tasks;
using TableCloth.Events;

namespace Spork.Commands.AboutWindow
{
    public sealed class AboutWindowCloseCommand : ViewModelCommandBase<AboutWindowViewModel>, IAsyncCommand<AboutWindowViewModel>
    {
        public override void Execute(AboutWindowViewModel viewModel)
            => ExecuteAsync(viewModel).SafeFireAndForget();

        public async Task ExecuteAsync(AboutWindowViewModel viewModel)
            => await viewModel.RequestCloseAsync(this, new DialogRequestEventArgs(default));
    }
}
