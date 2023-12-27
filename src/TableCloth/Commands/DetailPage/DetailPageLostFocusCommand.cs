using System;
using TableCloth.Components;
using TableCloth.Models;
using TableCloth.ViewModels;

namespace TableCloth.Commands.DetailPage;

public sealed class DetailPageLostFocusCommand : CommandBase
{
    public DetailPageLostFocusCommand(
        NavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    private readonly NavigationService _navigationService;

    public override void Execute(object? parameter)
    {
        if (parameter is not DetailPageViewModel viewModel)
            throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

        _navigationService.NavigateToCatalog(new CatalogPageArgumentModel()
        {
            SearchKeyword = viewModel.SearchKeyword,
        });
    }
}
