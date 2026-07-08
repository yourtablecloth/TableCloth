using System;
using System.Windows;
using TableCloth.ViewModels;

namespace TableCloth.Dialogs;

public partial class PowerSchemeGuideWindow : Window
{
    public PowerSchemeGuideWindow(
        PowerSchemeGuideWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += ViewModel_CloseRequested;
    }

    public PowerSchemeGuideWindowViewModel ViewModel
        => (PowerSchemeGuideWindowViewModel)DataContext;

    private void ViewModel_CloseRequested(object? sender, EventArgs e)
        => Close();
}
