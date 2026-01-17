using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace TableCloth.Dialogs;

/// <summary>
/// Interaction logic for SiteReportWindow.xaml
/// </summary>
public partial class SiteReportWindow : Window
{
    private const string GoogleFormsUrl = "https://forms.gle/28ZTZyorVCYd4N8F6";
    private const string GitHubIssueUrl = "https://github.com/yourtablecloth/TableClothCatalog/issues/new";

    public SiteReportWindow()
    {
        InitializeComponent();
    }

    private void GoogleFormsButton_Click(object sender, MouseButtonEventArgs e)
    {
        OpenUrl(GoogleFormsUrl);
    }

    private void GitHubIssueButton_Click(object sender, MouseButtonEventArgs e)
    {
        OpenUrl(GitHubIssueUrl);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch { }
    }
}
