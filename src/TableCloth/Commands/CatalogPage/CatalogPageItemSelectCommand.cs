using System;
using TableCloth.Components;
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
            throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

        if (viewModel.SelectedService == null)
            return;

        _navigationService.NavigateToDetail(viewModel.SelectedService, null);
    }
}
