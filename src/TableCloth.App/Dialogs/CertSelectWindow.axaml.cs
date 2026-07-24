using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using TableCloth.Events;
using TableCloth.ViewModels;

namespace TableCloth.Dialogs;

public partial class CertSelectWindow : Window
{
    public CertSelectWindow() => InitializeComponent();

    public CertSelectWindow(
        CertSelectWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += ViewModel_OnRequestClose;
        Loaded += OnLoaded;
    }

    public CertSelectWindowViewModel ViewModel
        => (CertSelectWindowViewModel)DataContext!;

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.CertSelectWindowLoadedCommand.CanExecute(ViewModel))
            ViewModel.CertSelectWindowLoadedCommand.Execute(ViewModel);
    }

    private void ViewModel_OnRequestClose(object? sender, DialogRequestEventArgs e)
        => Close(e.DialogResult);

    private void CertList_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel.SelectedCertPair != null)
            Close(true);
    }
}
