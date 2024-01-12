using TableCloth.Events;
using TableCloth.ViewModels;

namespace TableCloth.Commands.CertSelectWindow;

public sealed class CertSelectWindowRequestCancelCommand : ViewModelCommandBase<CertSelectWindowViewModel>
{
    public override void Execute(CertSelectWindowViewModel viewModel)
        => viewModel.RequestClose(this, new DialogRequestEventArgs(false));
}
