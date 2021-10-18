using System.Diagnostics;
using System.Windows;
using TableCloth.Resources;

namespace Hostess
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AboutContent.Text = StringResources.Get_AboutDialog_BodyText();
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
