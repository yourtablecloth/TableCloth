using Hostess.ViewModels;
using TableCloth.Events;

namespace Hostess.Commands.AboutWindow
{
    public sealed class AboutWindowCloseCommand : ViewModelCommandBase<AboutWindowViewModel>
    {
        public override async void Execute(AboutWindowViewModel viewModel)
            => await viewModel.RequestCloseAsync(this, new DialogRequestEventArgs(default));
    }
}
