namespace TableCloth.Commands;

public sealed class CertSelectWindowLoadedCommand : CommandBase
{
    public CertSelectWindowLoadedCommand(
        ScanCertPairCommand scanCertPairCommand)
    {
        _scanCertPairCommand = scanCertPairCommand;
    }

    private readonly ScanCertPairCommand _scanCertPairCommand;

    public override void Execute(object? parameter)
    {
        if (_scanCertPairCommand.CanExecute(parameter))
            _scanCertPairCommand.Execute(parameter);
    }
}
