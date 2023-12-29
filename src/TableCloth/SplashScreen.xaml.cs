using System.Windows;
using System.Windows.Input;
using TableCloth.Events;
using TableCloth.ViewModels;

namespace TableCloth;

/// <summary>
/// Interaction logic for SplashScreen.xaml
/// </summary>
public partial class SplashScreen : Window
{
    public SplashScreen(
        SplashScreenViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.StatusUpdate += ViewModel_StatusUpdate;
    }

    public SplashScreenViewModel ViewModel
        => (SplashScreenViewModel)DataContext;

    private void ViewModel_StatusUpdate(object? sender, StatusUpdateRequestEventArgs e)
    {
        ViewModel.Status = e.Status;
    }

    private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // https://stackoverflow.com/a/7418629
        if (e.ChangedButton == MouseButton.Left)
            this.DragMove();
    }
}
