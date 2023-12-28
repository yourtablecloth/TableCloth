using System;
using TableCloth.Components;
using TableCloth.Models;
using TableCloth.ViewModels;

namespace TableCloth.Commands.CatalogPage;

public sealed class CatalogPageItemSelectCommand : CommandBase
{
    public CatalogPageItemSelectCommand(
        NavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    private readonly NavigationService _navigationService;

    public override void Execute(object? parameter)
    {
        if (parameter is not CatalogPageViewModel viewModel)
            throw new ArgumentException(nameof(viewModel));

        var selectedServiceId = viewModel.SelectedService?.Id;
        var searchKeyword = viewModel.SearchKeyword;

        if (string.IsNullOrWhiteSpace(selectedServiceId))
            return;

        _navigationService.NavigateToDetail(new DetailPageArgumentModel(new string[] { selectedServiceId }, builtFromCommandLine: false)
        {
            SearchKeyword = searchKeyword,
        });
    }
}
