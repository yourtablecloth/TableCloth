using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.DetailPage;

public sealed class DetailPageSearchTextLostFocusCommand(
    INavigationService navigationService) : ViewModelCommandBase<DetailPageViewModel>
{
    public override void Execute(DetailPageViewModel viewModel)
        => navigationService.NavigateToCatalog(viewModel.SearchKeyword);
}
