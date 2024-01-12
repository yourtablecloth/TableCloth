using TableCloth.Events;
using TableCloth.ViewModels;

namespace TableCloth.Commands.CertSelectWindow;

public sealed class CertSelectWindowRequestConfirmCommand : ViewModelCommandBase<CertSelectWindowViewModel>
{
    public override void Execute(CertSelectWindowViewModel viewModel)
    {
        if (viewModel.SelectedCertPair != null)
            viewModel.RequestClose(this, new DialogRequestEventArgs(viewModel.SelectedCertPair != null));
    }
}
