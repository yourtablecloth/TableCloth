using System;
using TableCloth.ViewModels;

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
        if (parameter is not CertSelectWindowViewModel viewModel)
            throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

        if (_scanCertPairCommand.CanExecute(parameter))
            _scanCertPairCommand.Execute(parameter);
    }
}
