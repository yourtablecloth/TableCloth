using System;
using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.Shared;

public sealed class CertSelectCommand : ViewModelCommandBase<ITableClothViewModel>
{
    public CertSelectCommand(
        AppUserInterface appUserInterface)
    {
        _appUserInterface = appUserInterface;
    }

    private readonly AppUserInterface _appUserInterface;

    public AppUserInterface AppUserInterface
        => _appUserInterface;

    public override void Execute(ITableClothViewModel viewModel)
    {
        var certSelectWindow = _appUserInterface.CreateCertSelectWindow();
        var response = certSelectWindow.ShowDialog();

        if (!response.HasValue || !response.Value)
            return;

        if (certSelectWindow.ViewModel.SelectedCertPair != null)
            viewModel.SelectedCertFile = certSelectWindow.ViewModel.SelectedCertPair;
    }
}
