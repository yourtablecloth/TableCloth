using System;
using TableCloth.Components;
using TableCloth.Events;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.InputPasswordWindow;

public sealed class InputPasswordWindowConfirmCommand : ViewModelCommandBase<InputPasswordWindowViewModel>
{
    public InputPasswordWindowConfirmCommand(
        IX509CertPairScanner certPairScanner,
        IAppMessageBox appMessageBox)
    {
        _certPairScanner = certPairScanner;
        _appMessageBox = appMessageBox;
    }

    private readonly IX509CertPairScanner _certPairScanner;
    private readonly IAppMessageBox _appMessageBox;

    public override void Execute(InputPasswordWindowViewModel viewModel)
    {
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
