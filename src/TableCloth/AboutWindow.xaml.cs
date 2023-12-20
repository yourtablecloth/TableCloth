using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using TableCloth.Components;
using TableCloth.Resources;

namespace TableCloth
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow(
            AppMessageBox appMessageBox,
            CatalogDeserializer catalogDeserializer,
            ResourceResolver resourceResolver,
            LicenseDescriptor licenseDescriptor)
        {
            this._appMessageBox = appMessageBox;
            this._catalogDeserializer = catalogDeserializer;
            this._resourceResolver = resourceResolver;
            this._licenseDescriptor = licenseDescriptor;

            InitializeComponent();
        }

        private readonly AppMessageBox _appMessageBox;
        private readonly CatalogDeserializer _catalogDeserializer;
        private readonly ResourceResolver _resourceResolver;
        private readonly LicenseDescriptor _licenseDescriptor;

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AppVersionLabel.Content = StringResources.Get_AppVersion();
            CatalogDateLabel.Content = _catalogDeserializer.CatalogLastModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? StringResources.UnknownText;
            LicenseDetails.Text = await _licenseDescriptor.GetLicenseDescriptions();
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
                _appMessageBox.DisplayError(StringResources.Error_Cannot_Run_SysInfo, false);
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

                if (Version.TryParse(await _resourceResolver.GetLatestVersion(owner, repo), out Version? parsedVersion) &&
                    thisVersion != null && parsedVersion > thisVersion)
                {
                    _appMessageBox.DisplayInfo(StringResources.Info_UpdateRequired);
                    var targetUrl = await _resourceResolver.GetDownloadUrl(owner, repo);
                    var psi = new ProcessStartInfo(targetUrl.AbsoluteUri) { UseShellExecute = true, };
                    Process.Start(psi);
                    return;
                }
            }
            catch { }

            _appMessageBox.DisplayInfo(StringResources.Info_UpdateNotRequired);
        }

        private void OpenPrivacyHyperlink_Click(object sender, RoutedEventArgs e)
        {
            var psi = new ProcessStartInfo(StringResources.PrivacyPolicyUrl) { UseShellExecute = true };
            Process.Start(psi);
        }
    }
}
