using TableCloth.ViewModels;

namespace TableCloth.Commands.CertSelectWindow;

public sealed class CertSelectWindowLoadedCommand(
    CertSelectWindowScanCertPairCommand scanCertPairCommand) : ViewModelCommandBase<CertSelectWindowViewModel>
{
    public override void Execute(CertSelectWindowViewModel viewModel)
    {
        if (scanCertPairCommand.CanExecute(viewModel))
            scanCertPairCommand.Execute(viewModel);
    }
}
