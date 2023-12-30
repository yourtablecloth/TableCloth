using TableCloth.ViewModels;

namespace TableCloth.Commands.CertSelectWindow;

public sealed class CertSelectWindowLoadedCommand : ViewModelCommandBase<CertSelectWindowViewModel>
{
    public CertSelectWindowLoadedCommand(
        CertSelectWindowScanCertPairCommand scanCertPairCommand)
    {
        _scanCertPairCommand = scanCertPairCommand;
    }

    private readonly CertSelectWindowScanCertPairCommand _scanCertPairCommand;

    public override void Execute(CertSelectWindowViewModel viewModel)
    {
        if (_scanCertPairCommand.CanExecute(viewModel))
            _scanCertPairCommand.Execute(viewModel);
    }
}
