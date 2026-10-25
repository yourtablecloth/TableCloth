using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using TableCloth.Controls;
using TableCloth.ViewModels;

namespace TableCloth.Dialogs;

public partial class OptionsWindow : Window
{
    public OptionsWindow() => InitializeComponent();

    // 이슈 #296: 모달 다이얼로그 최소화 버튼 제거는 DialogHost.ShowModal 공통 진입점에서 일괄 처리.

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

    private async void OpenSkillMarketplace_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not OptionsWindowViewModel viewModel || viewModel.CreateSkillMarketplaceViewModel() is not { } marketplace) return;
        var window = new SkillMarketplaceWindow(marketplace);
        WindowChrome.RemoveMinimizeBox(window);
        await window.ShowDialog(this);
        await marketplace.WaitForLocalOperationAsync();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();
}
