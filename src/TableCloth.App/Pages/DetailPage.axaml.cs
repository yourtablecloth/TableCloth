using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System;
using TableCloth.ViewModels;

namespace TableCloth.Pages;

public partial class DetailPage : UserControl
{
    public DetailPage() => InitializeComponent();

    public DetailPage(
        DetailPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += ViewModel_CloseRequested;
        Loaded += OnLoaded;
    }

    public DetailPageViewModel ViewModel
        => (DetailPageViewModel)DataContext!;

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.DetailPageLoadedCommand.CanExecute(ViewModel))
            ViewModel.DetailPageLoadedCommand.Execute(ViewModel);
    }

    private void ViewModel_CloseRequested(object? sender, EventArgs e)
        => (this.GetVisualRoot() as Window)?.Close();

    private void SearchBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        // 이슈 #296: WPF Interaction.Triggers(LostFocus) → 코드비하인드. 검색어로 카탈로그로 되돌아간다.
        if (ViewModel.DetailPageSearchTextLostFocusCommand.CanExecute(ViewModel))
            ViewModel.DetailPageSearchTextLostFocusCommand.Execute(ViewModel);
    }
}
