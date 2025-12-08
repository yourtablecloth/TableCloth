using Spork.ViewModels;
using System;
using System.Windows;

namespace Spork
{
    public partial class MainWindow : Window
    {
        public MainWindow(
            MainWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.WindowLoaded += ViewModel_WindowLoaded;
            viewModel.CloseRequested += ViewModel_CloseRequested;
        }

        public MainWindowViewModel ViewModel
            => (MainWindowViewModel)DataContext;

        private void ViewModel_WindowLoaded(object sender, EventArgs e)
        {
            Width = MinWidth;
            Height = SystemParameters.PrimaryScreenHeight * 0.5;
            Top = 0;
            Left = SystemParameters.PrimaryScreenWidth - Width;
        }

        private void ViewModel_CloseRequested(object sender, EventArgs e)
        {
            Close();
        }

        private void OnLanguageToggleButtonClick(object sender, RoutedEventArgs e)
        {
            var current = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.StartsWith("ko") ? "en-US" : "ko-KR";
            var culture = new System.Globalization.CultureInfo(current);
            System.Threading.Thread.CurrentThread.CurrentCulture = culture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
            if (ViewModel != null)
            {
                ViewModel.LanguageToggleButtonText = current.StartsWith("ko") ? "To English" : "한글로";
            }
            Application.Current.Shutdown();
        }
    }
}
