using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using TableCloth.ViewModels;

namespace TableCloth.Dialogs;

public partial class OptionsWindow : Window
{
    public OptionsWindow() => InitializeComponent();

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // 이슈 #296: 모달 옵션 창에 최소화 버튼은 어색하므로 제거. Avalonia 에는 전용 플래그가 없어(Windows 전용 앱이므로)
        // Win32 로 WS_MINIMIZEBOX 스타일 비트를 해제한다(닫기 버튼은 유지). CanResize="False" 로 최대화는 이미 비활성.
        var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (hwnd != IntPtr.Zero)
        {
            var style = NativeMethods.GetWindowLongW(hwnd, NativeMethods.GWL_STYLE);
            NativeMethods.SetWindowLongW(hwnd, NativeMethods.GWL_STYLE, style & ~NativeMethods.WS_MINIMIZEBOX);
        }
    }

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
