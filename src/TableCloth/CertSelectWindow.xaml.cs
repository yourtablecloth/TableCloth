using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using TableCloth.ViewModels;

namespace TableCloth.Implementations.WPF
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
                ViewModel.SelectedCertPair = ViewModel.CertPairScanner.CreateX509CertPair(
                    ofd.FileNames.Where(x => string.Equals(".der", Path.GetExtension(x), StringComparison.OrdinalIgnoreCase)).SingleOrDefault(),
                    ofd.FileNames.Where(x => string.Equals(".key", Path.GetExtension(x), StringComparison.OrdinalIgnoreCase)).SingleOrDefault()
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
