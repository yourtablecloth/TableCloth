using System;
using TableCloth.Components;
using TableCloth.Events;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.InputPasswordWindow;

public sealed class InputPasswordWindowConfirmCommand : CommandBase
{
    public InputPasswordWindowConfirmCommand(
        X509CertPairScanner certPairScanner,
        AppMessageBox appMessageBox)
    {
        _certPairScanner = certPairScanner;
        _appMessageBox = appMessageBox;
    }

    private readonly X509CertPairScanner _certPairScanner;
    private readonly AppMessageBox _appMessageBox;

    public override void Execute(object? parameter)
    {
        if (parameter is not InputPasswordWindowViewModel viewModel)
            throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

        try
        {
            if (viewModel.PfxFilePath == null)
                throw new InvalidOperationException(StringResources.Error_Cannot_Find_PfxFile);

            var certPair = _certPairScanner.CreateX509Cert(viewModel.PfxFilePath, viewModel.Password);

            if (certPair != null)
                viewModel.ValidatedCertPair = certPair;

            viewModel.RequestClose(this, new DialogRequestEventArgs(true));
        }
        catch (Exception ex)
        {
            _appMessageBox.DisplayError(ex, false);
        }
    }
}
