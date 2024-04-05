using System.Security.Cryptography.X509Certificates;
using System.Windows;
using TableCloth.Events;
using TableCloth.Models.Configuration;
using TableCloth.ViewModels;

namespace TableCloth.Dialogs;

public partial class CertSelectWindow : Window
{
    public CertSelectWindow(
        CertSelectWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += ViewModel_OnRequestClose;
    }

    public CertSelectWindowViewModel ViewModel
        => (CertSelectWindowViewModel)DataContext;

    private void ViewModel_OnRequestClose(object? sender, DialogRequestEventArgs e)
    {
        this.DialogResult = e.DialogResult;
        this.Close();
    }

    private void ListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        switch (e.OriginalSource)
        {
            case FrameworkElement fe when fe.DataContext is X509CertPair && ViewModel.SelectedCertPair != null:
                DialogResult = true;
                Close();
                break;
        }
    }
}
