namespace TableCloth.Commands.CertSelectWindow;

public sealed class CertSelectWindowLoadedCommand : CommandBase
{
    public CertSelectWindowLoadedCommand(
        CertSelectWindowScanCertPairCommand scanCertPairCommand)
    {
        _scanCertPairCommand = scanCertPairCommand;
    }

    private readonly CertSelectWindowScanCertPairCommand _scanCertPairCommand;

    public override void Execute(object? parameter)
    {
        if (_scanCertPairCommand.CanExecute(parameter))
            _scanCertPairCommand.Execute(parameter);
    }
}
