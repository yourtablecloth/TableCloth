using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using TableCloth.ViewModels;

namespace TableCloth.Dialogs;

public partial class OptionsWindow : Window
{
    public OptionsWindow() => InitializeComponent();

    public OptionsWindow(OptionsWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += ViewModel_CloseRequested;
        Loaded += OnLoaded;
    }

    public OptionsWindowViewModel ViewModel
        => (OptionsWindowViewModel)DataContext!;

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.OptionsWindowLoadedCommand.CanExecute(ViewModel))
            ViewModel.OptionsWindowLoadedCommand.Execute(ViewModel);
    }

    private void ViewModel_CloseRequested(object? sender, EventArgs e) => Close();

    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();
}
