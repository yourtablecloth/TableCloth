using System;
using TableCloth.Components;
using TableCloth.Events;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.InputPasswordWindow;

public sealed class InputPasswordWindowConfirmCommand(
    IX509CertPairScanner certPairScanner,
    IAppMessageBox appMessageBox) : ViewModelCommandBase<InputPasswordWindowViewModel>
{
    public override async void Execute(InputPasswordWindowViewModel viewModel)
    {
        try
        {
            if (viewModel.PfxFilePath == null)
                throw new InvalidOperationException(ErrorStrings.Error_Cannot_Find_PfxFile);

            var certPair = certPairScanner.CreateX509Cert(viewModel.PfxFilePath, viewModel.Password);

            if (certPair != null)
                viewModel.ValidatedCertPair = certPair;

            await viewModel.RequestCloseAsync(this, new DialogRequestEventArgs(true));
        }
        catch (Exception ex)
        {
            appMessageBox.DisplayError(ex, false);
        }
    }
}
