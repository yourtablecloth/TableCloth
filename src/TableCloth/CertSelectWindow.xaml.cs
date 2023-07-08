using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using TableCloth.ViewModels;

namespace TableCloth
{
    public partial class CertSelectWindow : Window
    {
        public CertSelectWindow()
            => InitializeComponent();

        public CertSelectWindowViewModel ViewModel
            => (CertSelectWindowViewModel)DataContext;

        private void RefreshCertPairsButton_Click(object sender, RoutedEventArgs e)
            => ViewModel.RefreshCertPairs();

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

            var npkiPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "NPKI");
            var userDirectories = new List<string>();

            if (Directory.Exists(npkiPath))
                userDirectories.AddRange(Directory.GetDirectories(npkiPath, "USER", SearchOption.AllDirectories));

            var removableDrives = DriveInfo.GetDrives().Where(x => x.DriveType == DriveType.Removable).Select(x => x.RootDirectory.FullName);

            ofd.CustomPlaces = new string[] { npkiPath, }
                .Concat(userDirectories)
                .Concat(removableDrives)
                .Where(x => Directory.Exists(x))
                .Select(x => new FileDialogCustomPlace(x))
                .ToList();

            var response = ofd.ShowDialog();

            if (response.HasValue && response.Value)
            {
                switch (ofd.FilterIndex)
                {
                    case 1:
                        var firstFilePath = ofd.FileNames.FirstOrDefault();

                        if (string.IsNullOrWhiteSpace(firstFilePath) || !File.Exists(firstFilePath))
                            return;

                        var basePath = Path.GetDirectoryName(firstFilePath);
                        var signCertDerPath = Path.Combine(basePath, "signCert.der");
                        var signPriKeyPath = Path.Combine(basePath, "signPri.key");

                        if (!File.Exists(signCertDerPath) && !File.Exists(signPriKeyPath))
                            return;

                        ViewModel.SelectedCertPair = ViewModel.CertPairScanner.CreateX509CertPair(signCertDerPath, signPriKeyPath);
                        DialogResult = true;
                        Close();
                        break;

                    case 2:
                        var pfxFilePath = ofd.FileNames.FirstOrDefault();

                        if (string.IsNullOrWhiteSpace(pfxFilePath) || !File.Exists(pfxFilePath))
                            return;

                        var inputWindow = new InputPasswordWindow()
                        {
                            PfxFilePath = pfxFilePath,
                            Owner = this,
                        };

                        var inputPwdResult = inputWindow.ShowDialog();

                        if (!inputPwdResult.HasValue || !inputPwdResult.Value || inputWindow.ValidatedCertPair == null)
                            return;

                        ViewModel.SelectedCertPair = inputWindow.ValidatedCertPair;
                        DialogResult = true;
                        Close();
                        break;
                }
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

        private void ConvertToPfxButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
