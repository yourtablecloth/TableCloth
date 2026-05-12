using System;
using System.Windows;
using System.Windows.Interop;
using TableCloth.ViewModels;

namespace TableCloth.Dialogs;

public partial class DisclaimerWindow : Window
{
    public DisclaimerWindow(
        DisclaimerWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.ViewLoaded += ViewModel_ViewLoaded;
        viewModel.DisclaimerAcknowledged += ViewModel_DisclaimerAcknowledged;
    }

    public DisclaimerWindowViewModel ViewModel
        => (DisclaimerWindowViewModel)DataContext;

    private void ViewModel_ViewLoaded(object? sender, EventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var hwnd = new WindowInteropHelper(this).Handle;

        _ = NativeMethods.SetWindowLongW(
            hwnd,
            NativeMethods.GWL_STYLE,
            NativeMethods.GetWindowLongW(hwnd, NativeMethods.GWL_STYLE) & ~NativeMethods.WS_SYSMENU);
    }

    private void ViewModel_DisclaimerAcknowledged(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
