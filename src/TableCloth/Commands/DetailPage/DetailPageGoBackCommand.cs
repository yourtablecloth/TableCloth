using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.DetailPage;

public sealed class DetailPageGoBackCommand : ViewModelCommandBase<DetailPageViewModel>
{
    public DetailPageGoBackCommand(
        NavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    private readonly NavigationService _navigationService;

    public override void Execute(DetailPageViewModel viewModel)
        => _navigationService.NavigateToCatalog(string.Empty);
}
