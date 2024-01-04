using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.CatalogPage;

public sealed class CatalogPageItemSelectCommand(
    INavigationService navigationService) : ViewModelCommandBase<CatalogPageViewModel>
{
    public override void Execute(CatalogPageViewModel viewModel)
    {
        if (viewModel.SelectedService == null)
            return;

        navigationService.NavigateToDetail(viewModel.SelectedService, null);
    }
}
