using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.Shared;

public sealed class CertSelectCommand : ViewModelCommandBase<ITableClothViewModel>
{
    public CertSelectCommand(
        IAppUserInterface appUserInterface)
    {
        _appUserInterface = appUserInterface;
    }

    private readonly IAppUserInterface _appUserInterface;

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
