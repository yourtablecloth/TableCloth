using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.DetailPage;

public sealed class DetailPageSearchTextLostFocusCommand : ViewModelCommandBase<DetailPageViewModel>
{
    public DetailPageSearchTextLostFocusCommand(
        INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    private readonly INavigationService _navigationService;

    public override void Execute(DetailPageViewModel viewModel)
        => _navigationService.NavigateToCatalog(viewModel.SearchKeyword);
}
