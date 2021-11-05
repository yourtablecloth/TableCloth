using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
        }

        public AboutWindowViewModel ViewModel
            => (AboutWindowViewModel)DataContext;

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AppVersionLabel.Content = StringResources.Get_AppVersion();
            CatalogDateLabel.Content = ViewModel.CatalogVersion?.ToString("yyyy-MM-dd HH:mm:ss") ?? StringResources.UnknownText;
            LicenseDetails.Text = await LicenseDescriptor.GetLicenseDescriptions();
        }

        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenWebsiteButton_Click(object sender, RoutedEventArgs e)
        {
            var psi = new ProcessStartInfo(StringResources.AppInfoUrl) { UseShellExecute = true };
            Process.Start(psi);
        }

        private void ShowSysInfo_Click(object sender, RoutedEventArgs e)
        {
            var msinfoPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "msinfo32.exe");

            if (!File.Exists(msinfoPath))
            {
                ViewModel.AppMessageBox.DisplayError(this, StringResources.Error_Cannot_Run_SysInfo, false);
                return;
            }

            var psi = new ProcessStartInfo(msinfoPath);
            Process.Start(psi);
        }

        private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var owner = "yourtablecloth";
                var repo = "TableCloth";
                var thisVersion = GetType().Assembly.GetName().Version;

                if (Version.TryParse(await GitHubReleaseFinder.GetLatestVersion(owner, repo), out Version parsedVersion) &&
                    thisVersion != null && parsedVersion > thisVersion)
                {
                    ViewModel.AppMessageBox.DisplayInfo(this, StringResources.Info_UpdateRequired);
                    var targetUrl = await GitHubReleaseFinder.GetDownloadUrl(owner, repo);
                    var psi = new ProcessStartInfo(targetUrl.AbsoluteUri) { UseShellExecute = true, };
                    Process.Start(psi);
                    return;
                }
            }
            catch { }

            ViewModel.AppMessageBox.DisplayInfo(this, StringResources.Info_UpdateNotRequired);
        }
    }
}
