using TableCloth.Events;
using TableCloth.ViewModels;

namespace TableCloth.Commands.CertSelectWindow;

public sealed class CertSelectWindowRequestConfirmCommand : ViewModelCommandBase<CertSelectWindowViewModel>
{
    public override async void Execute(CertSelectWindowViewModel viewModel)
    {
        if (viewModel.SelectedCertPair != null)
            await viewModel.RequestCloseAsync(this, new DialogRequestEventArgs(viewModel.SelectedCertPair != null));
    }
}
