using Hostess.ViewModels;
using TableCloth.Events;

namespace Hostess.Commands.PrecautionsWindow
{
    public sealed class PrecautionsWindowCloseCommand : ViewModelCommandBase<PrecautionsWindowViewModel>
    {
        public override async void Execute(PrecautionsWindowViewModel viewModel)
            => await viewModel.RequestCloseAsync(this, new DialogRequestEventArgs(true));
    }
}
