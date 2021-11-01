using System.Diagnostics;
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AppVersionLabel.Content = StringResources.Get_AppVersion();
            CatalogDateLabel.Content = ViewModel.CatalogVersion?.ToString("yyyy-MM-dd HH:mm:ss") ?? StringResources.UnknownText;
            LicenseDetails.Text = "To Do";
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
