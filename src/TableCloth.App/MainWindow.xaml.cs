using System.Windows;
using TableCloth.ViewModels;

namespace TableCloth;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(
        MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public MainWindowViewModel ViewModel
        => (MainWindowViewModel)DataContext;
}
