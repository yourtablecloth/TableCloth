using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Security;
using System.Text;
using TableCloth.Events;
using TableCloth.ViewModels;

namespace TableCloth.Dialogs;

public partial class InputPasswordWindow : Window
{
    public InputPasswordWindow() => InitializeComponent();

    public InputPasswordWindow(
        InputPasswordWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.ViewLoaded += ViewModel_ViewLoaded;
        viewModel.CloseRequested += ViewModel_CloseRequested;
        viewModel.RetryPasswordInputRequested += ViewModel_RetryPasswordInputRequested;
    }

    public InputPasswordWindowViewModel ViewModel
        => (InputPasswordWindowViewModel)DataContext!;

    private void ViewModel_ViewLoaded(object? sender, EventArgs e)
    {
        var lines = new StringBuilder();
        lines.AppendLine(string.Format((string)CertInformation.Tag!, ViewModel.PfxFilePath));
        CertInformation.Text = lines.ToString();
        PasswordInput.Focus();
    }

    private void ViewModel_CloseRequested(object? sender, DialogRequestEventArgs e)
        => Close(e.DialogResult);

    private void ViewModel_RetryPasswordInputRequested(object? sender, EventArgs e)
        => PasswordInput.Focus();

    private void PasswordInput_TextChanged(object? sender, TextChangedEventArgs e)
    {
        // 이슈 #296: WPF PasswordBox.SecurePassword → Avalonia TextBox(PasswordChar) 텍스트로 SecureString 구성.
        var secure = new SecureString();
        foreach (var ch in PasswordInput.Text ?? string.Empty)
            secure.AppendChar(ch);
        secure.MakeReadOnly();
        ViewModel.Password = secure;
    }
}
