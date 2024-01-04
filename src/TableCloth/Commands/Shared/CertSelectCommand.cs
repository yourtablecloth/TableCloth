using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.Shared;

public sealed class CertSelectCommand(
    IAppUserInterface appUserInterface) : ViewModelCommandBase<ITableClothViewModel>
{
    public override void Execute(ITableClothViewModel viewModel)
    {
        var certSelectWindow = appUserInterface.CreateCertSelectWindow();
        var response = certSelectWindow.ShowDialog();

        if (!response.HasValue || !response.Value)
            return;

        if (certSelectWindow.ViewModel.SelectedCertPair != null)
            viewModel.SelectedCertFile = certSelectWindow.ViewModel.SelectedCertPair;
    }
}
