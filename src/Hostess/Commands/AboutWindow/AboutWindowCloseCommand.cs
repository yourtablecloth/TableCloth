using Hostess.ViewModels;

namespace Hostess.Commands.AboutWindow
{
    public sealed class AboutWindowCloseCommand : ViewModelCommandBase<AboutWindowViewModel>
    {
        public override void Execute(AboutWindowViewModel viewModel)
            => viewModel.RequestClose(this, default);
    }
}
