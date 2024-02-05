using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using System;
using System.Threading.Tasks;
using TableCloth.Components;
using TableCloth.Events;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.InputPasswordWindow;

public sealed class InputPasswordWindowConfirmCommand(
    IX509CertPairScanner certPairScanner,
    IAppMessageBox appMessageBox) : ViewModelCommandBase<InputPasswordWindowViewModel>, IAsyncCommand<InputPasswordWindowViewModel>
{
    public override void Execute(InputPasswordWindowViewModel viewModel)
        => ExecuteAsync(viewModel).SafeFireAndForget();

    public async Task ExecuteAsync(InputPasswordWindowViewModel viewModel)
    {
        try
        {
            var pfxFilePath = viewModel.PfxFilePath.EnsureNotNull(ErrorStrings.Error_Cannot_Find_PfxFile);
            var certPair = certPairScanner.CreateX509Cert(pfxFilePath, viewModel.Password);

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
