using System.Diagnostics;
using System.Windows;
using TableCloth.Resources;

namespace Hostess.Dialogs
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

        public string CatalogDate { get; set; } = StringResources.UnknownText;

        public string License { get; set; } = StringResources.UnknownText;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AppVersionLabel.Content = StringResources.Get_AppVersion();
            CatalogDateLabel.Content = CatalogDate;
            LicenseDetails.Text = License;
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
    }
}
