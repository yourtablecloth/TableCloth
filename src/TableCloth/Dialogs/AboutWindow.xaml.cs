using System.Windows;
using TableCloth.ViewModels;

namespace TableCloth.Dialogs;

/// <summary>
/// Interaction logic for AboutWindow.xaml
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow(
        AboutWindowViewModel aboutWindowViewModel)
    {
        InitializeComponent();
        DataContext = aboutWindowViewModel;
    }

    public AboutWindowViewModel ViewModel
        => (AboutWindowViewModel)DataContext;

    private void OkayButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
