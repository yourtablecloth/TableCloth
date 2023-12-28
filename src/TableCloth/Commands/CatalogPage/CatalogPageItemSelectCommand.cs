using System;
using TableCloth.Components;
using TableCloth.Models;
using TableCloth.Models.Catalog;
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
        var selectedServiceId = default(string);
        var searchKeyword = string.Empty;

        switch (parameter)
        {
            case CatalogPageViewModel viewModel:
                selectedServiceId = viewModel.SelectedService?.Id;
                searchKeyword = viewModel.SearchKeyword;
                break;

            case CatalogInternetService service:
                selectedServiceId = service.Id;
                break;

            default:
                throw new ArgumentException(nameof(parameter));
        }

        if (string.IsNullOrWhiteSpace(selectedServiceId))
            return;

        _navigationService.NavigateToDetail(new DetailPageArgumentModel(new string[] { selectedServiceId }, builtFromCommandLine: false)
        {
            SearchKeyword = searchKeyword,
        });
    }
}
