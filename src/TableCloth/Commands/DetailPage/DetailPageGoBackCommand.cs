using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.DetailPage;

public sealed class DetailPageGoBackCommand(
    INavigationService navigationService) : ViewModelCommandBase<DetailPageViewModel>
{
    public override void Execute(DetailPageViewModel viewModel)
        => navigationService.GoBack();
}
