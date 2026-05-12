using System;
using System.Windows;
using TableCloth.ViewModels;

namespace TableCloth.Dialogs;

public partial class OptionsWindow : Window
{
    public OptionsWindow(OptionsWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += ViewModel_CloseRequested;
    }

    public OptionsWindowViewModel ViewModel
        => (OptionsWindowViewModel)DataContext;

    private void ViewModel_CloseRequested(object? sender, EventArgs e) => Close();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
