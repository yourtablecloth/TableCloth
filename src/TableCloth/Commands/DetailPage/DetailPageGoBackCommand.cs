using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.DetailPage;

public sealed class DetailPageGoBackCommand : ViewModelCommandBase<DetailPageViewModel>
{
    public DetailPageGoBackCommand(
        INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    private readonly INavigationService _navigationService;

    public override void Execute(DetailPageViewModel viewModel)
        => _navigationService.NavigateToCatalog(string.Empty);
}
