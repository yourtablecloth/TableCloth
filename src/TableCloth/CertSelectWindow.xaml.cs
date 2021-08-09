using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using TableCloth.ViewModels;

namespace TableCloth.Implementations.WPF
{
    public partial class CertSelectWindow : Window
    {
        public CertSelectWindow()
        {
            InitializeComponent();
        }

        public CertSelectWindowViewModel ViewModel
            => (CertSelectWindowViewModel)DataContext;

        private void RefreshCertPairsButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RefreshCertPairs();
        }

        private void OpenCertPairManuallyButton_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog()
            {
                AddExtension = true,
                CheckFileExists = true,
                CheckPathExists = true,
                DereferenceLinks = true,
                Filter = (string)Application.Current.Resources["CertSelectWindow_FileOpenDialog_FilterText"],
                FilterIndex = 0,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Multiselect = true,
                ReadOnlyChecked = true,
                ShowReadOnly = false,
                Title = (string)Application.Current.Resources["CertSelectWindow_FileOpenDialog_Text"],
                ValidateNames = true,
            };

            // To Do: NPKI 폴더 외에 이동식 드라이브, 사용자 인증서 폴더 등을 미리 추가할 수 있을지 검토 필요
            var npkiPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "NPKI");

            if (Directory.Exists(npkiPath))
            {
                ofd.CustomPlaces = new FileDialogCustomPlace[]
                {
                new FileDialogCustomPlace(npkiPath),
                };
            }

            var response = ofd.ShowDialog();

            if (response.HasValue && response.Value)
            {
                ViewModel.SelectedCertPair = ViewModel.CertPairScanner.CreateX509CertPair(
                    ofd.FileNames.Where(x => string.Equals(".der", System.IO.Path.GetExtension(x), StringComparison.OrdinalIgnoreCase)).First(),
                    ofd.FileNames.Where(x => string.Equals(".key", System.IO.Path.GetExtension(x), StringComparison.OrdinalIgnoreCase)).First()
                );
                DialogResult = true;
                Close();
                return;
            }
        }

        private void OkayButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = ViewModel.SelectedCertPair != null;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
