using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TableCloth.Implementations.WindowsSandbox;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Implementations.WPF
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindowViewModel ViewModel
            => (MainWindowViewModel)DataContext;

        private List<CatalogInternetService> _selectedSites = new();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var foundCandidate = ViewModel.CertPairScanner.ScanX509Pairs(ViewModel.CertPairScanner.GetCandidateDirectories()).SingleOrDefault();

            if (foundCandidate != null)
            {
                ViewModel.SelectedCertFiles = new string[]
                {
                    foundCandidate.DerFilePath,
                    foundCandidate.KeyFilePath,
                }.ToList();

                ViewModel.MapNpkiCert = true;
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var certSelectWindow = new CertSelectWindow();
            var response = certSelectWindow.ShowDialog();

            if (!response.HasValue || !response.Value)
                return;

            if (certSelectWindow.ViewModel.SelectedCertPair != null &&
                File.Exists(certSelectWindow.ViewModel.SelectedCertPair.DerFilePath) &&
                File.Exists(certSelectWindow.ViewModel.SelectedCertPair.KeyFilePath))
            {
                ViewModel.SelectedCertFiles = new string[]
                {
                    certSelectWindow.ViewModel.SelectedCertPair.DerFilePath,
                    certSelectWindow.ViewModel.SelectedCertPair.KeyFilePath,
                }.ToList();
            }
        }

        private void SiteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = (ListBox)e.Source;
            _selectedSites = listBox.SelectedItems.Cast<CatalogInternetService>().ToList();
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.AppMessageBox.DisplayInfo(this, StringResources.AboutDialog_BodyText);
        }

        private void LaunchSandboxButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SandboxLauncher.IsSandboxRunning())
            {
                ViewModel.AppMessageBox.DisplayError(this, StringResources.Error_Windows_Sandbox_Already_Running, false);
                return;
            }

            var pair = default(X509CertPair);
            var fileList = ViewModel.SelectedCertFiles;

            if (ViewModel.MapNpkiCert && fileList != null)
            {
                var derFilePath = fileList.Where(x => string.Equals(Path.GetExtension(x), ".der", StringComparison.OrdinalIgnoreCase)).SingleOrDefault();
                var keyFilePath = fileList.Where(x => string.Equals(Path.GetExtension(x), ".key", StringComparison.OrdinalIgnoreCase)).SingleOrDefault();

                if (File.Exists(derFilePath) && File.Exists(keyFilePath))
                    pair = ViewModel.CertPairScanner.CreateX509CertPair(derFilePath, keyFilePath);
            }

            var config = new TableClothConfiguration()
            {
                CertPair = pair,
                EnableMicrophone = ViewModel.EnableMicrophone,
                EnableWebCam = ViewModel.EnableWebCam,
                EnablePrinters = ViewModel.EnablePrinters,
                EnableEveryonesPrinter = ViewModel.EnableEveryonesPrinter,
                Companions = ViewModel.CatalogDocument.Companions,
                Services = _selectedSites,
            };

            var tempPath = ViewModel.SharedLocations.GetDataPath($"bwsb_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}");
            var excludedFolderList = new List<SandboxMappedFolder>();
            var wsbFilePath = ViewModel.SandboxBuilder.GenerateSandboxConfiguration(tempPath, config, excludedFolderList);

            if (excludedFolderList.Any())
                ViewModel.AppMessageBox.DisplayError(this, StringResources.Error_HostFolder_Unavailable(excludedFolderList.Select(x => x.HostFolder)), false);

            ViewModel.CurrentDirectory = tempPath;
            ViewModel.TemporaryDirectories.Add(tempPath);
            ViewModel.SandboxLauncher.RunSandbox(ViewModel.AppUserInterface, tempPath, wsbFilePath);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenExplorer(string targetDirectoryPath)
        {
            if (!Directory.Exists(targetDirectoryPath))
                return;

            var psi = new ProcessStartInfo(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                targetDirectoryPath)
            {
                UseShellExecute = false,
            };

            Process.Start(psi);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            foreach (var eachDirectory in ViewModel.TemporaryDirectories)
            {
                if (!string.IsNullOrWhiteSpace(ViewModel.CurrentDirectory))
                {
                    if (string.Equals(Path.GetFullPath(eachDirectory), Path.GetFullPath(ViewModel.CurrentDirectory), StringComparison.OrdinalIgnoreCase))
                    {
                        if (ViewModel.SandboxLauncher.IsSandboxRunning())
                        {
                            OpenExplorer(eachDirectory);
                            continue;
                        }
                    }
                }

                if (!Directory.Exists(eachDirectory))
                    continue;

                try { Directory.Delete(eachDirectory, true); }
                catch { OpenExplorer(eachDirectory); }
            }
        }
    }
}
