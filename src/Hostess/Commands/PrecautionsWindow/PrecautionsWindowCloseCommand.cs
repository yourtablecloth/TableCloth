using Hostess.ViewModels;

namespace Hostess.Commands.PrecautionsWindow
{
    public sealed class PrecautionsWindowCloseCommand : ViewModelCommandBase<PrecautionsWindowViewModel>
    {
        public override void Execute(PrecautionsWindowViewModel viewModel)
            => viewModel.RequestClose(this, true);
    }
}
