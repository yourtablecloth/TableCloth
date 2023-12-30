using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.CatalogPage;

public sealed class CatalogPageItemSelectCommand : ViewModelCommandBase<CatalogPageViewModel>
{
    public CatalogPageItemSelectCommand(
        NavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    private readonly NavigationService _navigationService;

    public override void Execute(CatalogPageViewModel viewModel)
    {
        if (viewModel.SelectedService == null)
            return;

        _navigationService.NavigateToDetail(viewModel.SelectedService, null);
    }
}
