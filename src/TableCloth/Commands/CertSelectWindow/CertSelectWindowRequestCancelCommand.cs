using TableCloth.Events;
using TableCloth.ViewModels;

namespace TableCloth.Commands.CertSelectWindow;

public sealed class CertSelectWindowRequestCancelCommand : ViewModelCommandBase<CertSelectWindowViewModel>
{
    public override async void Execute(CertSelectWindowViewModel viewModel)
        => await viewModel.RequestCloseAsync(this, new DialogRequestEventArgs(false));
}
