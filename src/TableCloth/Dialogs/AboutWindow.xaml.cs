using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
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

    private void SponsorBanner_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/sponsors/yourtablecloth",
                UseShellExecute = true,
            });
        }
        catch { }
    }
}
