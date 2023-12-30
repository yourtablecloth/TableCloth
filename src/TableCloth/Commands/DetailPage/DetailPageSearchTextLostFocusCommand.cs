using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.DetailPage;

public sealed class DetailPageSearchTextLostFocusCommand : ViewModelCommandBase<DetailPageViewModel>
{
    public DetailPageSearchTextLostFocusCommand(
        NavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    private readonly NavigationService _navigationService;

    public override void Execute(DetailPageViewModel viewModel)
        => _navigationService.NavigateToCatalog(viewModel.SearchKeyword);
}
