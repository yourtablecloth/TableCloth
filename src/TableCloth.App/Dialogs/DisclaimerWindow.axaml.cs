using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using TableCloth.ViewModels;

namespace TableCloth.Dialogs;

public partial class DisclaimerWindow : Window
{
    public DisclaimerWindow() => InitializeComponent();

    public DisclaimerWindow(
        DisclaimerWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.ViewLoaded += ViewModel_ViewLoaded;
        viewModel.DisclaimerAcknowledged += ViewModel_DisclaimerAcknowledged;
        Loaded += OnLoaded;
    }

    public DisclaimerWindowViewModel ViewModel
        => (DisclaimerWindowViewModel)DataContext!;

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.DisclaimerWindowLoadedCommand.CanExecute(ViewModel))
            ViewModel.DisclaimerWindowLoadedCommand.Execute(ViewModel);
    }

    private void ViewModel_ViewLoaded(object? sender, EventArgs e)
    {
        // 이슈 #296: WPF WS_SYSMENU 제거(닫기 버튼 숨김) 네이티브 트윅은 폐기(간소화). 창은 표준 크롬을 사용한다.
    }

    private void ViewModel_DisclaimerAcknowledged(object? sender, EventArgs e)
        => Close(true);
}
