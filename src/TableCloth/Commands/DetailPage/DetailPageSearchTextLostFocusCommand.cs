using System;
using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.DetailPage;

public sealed class DetailPageSearchTextLostFocusCommand : CommandBase
{
    public DetailPageSearchTextLostFocusCommand(
        NavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    private readonly NavigationService _navigationService;

    public override void Execute(object? parameter)
    {
        if (parameter is not DetailPageViewModel viewModel)
            throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

        _navigationService.NavigateToCatalog(viewModel.SearchKeyword);
    }
}
