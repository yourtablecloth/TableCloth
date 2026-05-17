using System;
using System.Text;
using System.Windows;
using TableCloth.Events;
using TableCloth.ViewModels;

namespace TableCloth.Dialogs;

public partial class InputPasswordWindow : Window
{
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
        => (InputPasswordWindowViewModel)DataContext;

    private void ViewModel_ViewLoaded(object? sender, EventArgs e)
    {
        var lines = new StringBuilder();
        lines.AppendLine(string.Format((string)CertInformation.Tag, ViewModel.PfxFilePath));
        CertInformation.Text = lines.ToString();
        PasswordInput.Focus();
    }

    private void ViewModel_CloseRequested(object? sender, DialogRequestEventArgs e)
    {
        DialogResult = e.DialogResult;
        Close();
    }

    private void ViewModel_RetryPasswordInputRequested(object? sender, EventArgs e)
    {
        PasswordInput.Focus();
    }

    private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
        => ViewModel.Password = PasswordInput.SecurePassword;
}
