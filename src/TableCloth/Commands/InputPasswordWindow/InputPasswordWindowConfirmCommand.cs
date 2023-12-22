using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        var viewModel = parameter as InputPasswordWindowViewModel;

        if (viewModel == null)
            throw new ArgumentException(nameof(parameter));

        try
        {
            if (viewModel.PfxFilePath == null)
                throw new InvalidOperationException(StringResources.Error_Cannot_Find_PfxFile);

            var certPair = _certPairScanner.CreateX509Cert(viewModel.PfxFilePath, viewModel.Password);

            if (certPair != null)
                viewModel.ValidatedCertPair = certPair;

            viewModel.OnRequestClose(this, new DialogRequestEventArgs(true));
        }
        catch (Exception ex)
        {
            _appMessageBox.DisplayError(ex, false);
        }
    }
}
