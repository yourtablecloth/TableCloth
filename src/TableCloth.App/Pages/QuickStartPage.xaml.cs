using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using TableCloth.ViewModels;

namespace TableCloth.Pages;

public partial class QuickStartPage : Page
{
    public QuickStartPage(QuickStartPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public QuickStartPageViewModel ViewModel
        => (QuickStartPageViewModel)DataContext;

    private void SponsorBanner_MouseLeftButtonUp(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://yourtablecloth.app/#sponsor",
                UseShellExecute = true,
            });
        }
        catch { }
    }
}
