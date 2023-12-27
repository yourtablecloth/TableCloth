using TableCloth.Components;

namespace TableCloth.Commands.DetailPage;

public sealed class DetailPageGoBackCommand : CommandBase
{
    public DetailPageGoBackCommand(
        NavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    private readonly NavigationService _navigationService;

    public override void Execute(object? parameter)
    {
        _navigationService.GoBack();
    }
}
