using System.Windows;
using TableCloth.ViewModels;

namespace TableCloth;

/// <summary>
/// Interaction logic for MainWindowV2.xaml
/// </summary>
public partial class MainWindowV2 : Window
{
    public MainWindowV2(MainWindowV2ViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public MainWindowV2ViewModel ViewModel
        => (MainWindowV2ViewModel)DataContext;
}
