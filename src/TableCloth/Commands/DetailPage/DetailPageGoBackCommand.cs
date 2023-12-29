using System;
using TableCloth.Components;
using TableCloth.ViewModels;

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
        if (parameter is not DetailPageViewModel /*viewModel*/)
            throw new ArgumentException("Uncompatible parameter type.", nameof(parameter));

        _navigationService.NavigateToCatalog(string.Empty);
    }
}
