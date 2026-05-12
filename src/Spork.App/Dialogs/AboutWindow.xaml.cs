using Spork.ViewModels;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using TableCloth.Events;

namespace Spork.Dialogs
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow(AboutWindowViewModel aboutWindowViewModel)
        {
            InitializeComponent();
            DataContext = aboutWindowViewModel;
            aboutWindowViewModel.CloseRequested += AboutWindowViewModel_CloseRequested;
        }

        private void AboutWindowViewModel_CloseRequested(object sender, DialogRequestEventArgs e)
        {
            DialogResult = e.DialogResult;
            Close();
        }

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
}
