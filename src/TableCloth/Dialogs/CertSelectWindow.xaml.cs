using System.Windows;
using TableCloth.Events;
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
}
