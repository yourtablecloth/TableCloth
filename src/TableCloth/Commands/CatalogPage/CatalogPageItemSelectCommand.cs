using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.CatalogPage;

public sealed class CatalogPageItemSelectCommand : ViewModelCommandBase<CatalogPageViewModel>
{
    public CatalogPageItemSelectCommand(
        INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    private readonly INavigationService _navigationService;

    public override void Execute(CatalogPageViewModel viewModel)
    {
        if (viewModel.SelectedService == null)
            return;

        _navigationService.NavigateToDetail(viewModel.SelectedService, null);
    }
}
