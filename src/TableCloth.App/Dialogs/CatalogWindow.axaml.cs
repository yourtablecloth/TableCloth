using Avalonia.Controls;
using Avalonia.Input;
using System;
using TableCloth.Models.Catalog;
using TableCloth.ViewModels;

namespace TableCloth.Dialogs;

public partial class CatalogWindow : Window
{
    public CatalogWindow() => InitializeComponent();

    public CatalogWindow(CatalogWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Closed += (_, _) => viewModel.CloseRequested -= OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();

    private void ServiceList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not CatalogWindowViewModel viewModel ||
            e.Source is not Control { DataContext: CatalogInternetService service })
            return;

        viewModel.SelectedService = service;
        viewModel.LaunchSelectedCommand.Execute(null);
    }
}
