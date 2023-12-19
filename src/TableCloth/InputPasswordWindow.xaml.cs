using System;
using System.Text;
using System.Windows;
using TableCloth.Models.Configuration;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth
{
    public partial class InputPasswordWindow : Window
    {
        public InputPasswordWindow(InputPasswordWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        public InputPasswordWindowViewModel ViewModel
            => (InputPasswordWindowViewModel)DataContext;

        public string PfxFilePath { get; set; }

        public X509CertPair ValidatedCertPair { get; private set; }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var lines = new StringBuilder();
            lines.AppendLine(string.Format((string)CertInformation.Tag, PfxFilePath));
            CertInformation.Text = lines.ToString();
            PasswordInput.Focus();
        }

        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var certPair = ViewModel.CertPairScanner.CreateX509Cert(PfxFilePath, PasswordInput.SecurePassword);

                if (certPair != null)
                    ValidatedCertPair = certPair;

                DialogResult = true;
            }
            catch (Exception ex)
            {
                ViewModel.AppMessageBox.DisplayError(ex, false);
                PasswordInput.Focus();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
