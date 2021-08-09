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

        private List<CatalogInternetService> _selectedSites = new ();

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
                var derFilePath = fileList.Where(x => string.Equals(Path.GetExtension(x), ".der", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                var keyFilePath = fileList.Where(x => string.Equals(Path.GetExtension(x), ".key", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                if (File.Exists(derFilePath) && File.Exists(keyFilePath))
                    pair = ViewModel.CertPairScanner.CreateX509CertPair(derFilePath, keyFilePath);
            }

            var config = new TableClothConfiguration()
            {
                CertPair = pair,
                EnableMicrophone = ViewModel.EnableMicrophone,
                EnableWebCam = ViewModel.EnableWebCam,
                EnablePrinters = ViewModel.EnablePrinters,
                Packages = _selectedSites,
            };

            var tempPath = Path.Combine(ViewModel.AppStartup.AppDataDirectoryPath, $"bwsb_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}");
            var excludedFolderList = new List<SandboxMappedFolder>();
            var wsbFilePath = ViewModel.SandboxBuilder.GenerateSandboxConfiguration(tempPath, config, excludedFolderList);

            if (excludedFolderList.Any())
                ViewModel.AppMessageBox.DisplayError(this, StringResources.Error_HostFolder_Unavailable(excludedFolderList.Select(x => x.HostFolder)), false);

            ViewModel.TemporaryDirectories.Add(tempPath);
            ViewModel.SandboxLauncher.RunSandbox(ViewModel.AppUserInterface, tempPath, wsbFilePath);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (!ViewModel.SandboxLauncher.IsSandboxRunning())
            {
                // To Do: 마지막으로 샌드박스를 띄웠던 폴더와 일치하지 않으면 모두 삭제하도록 로직 보완 필요
                // To Do: 디렉터리 삭제 실패 시 사용자에게 안내하도록 하는 로직 보완 필요
                foreach (var eachDirectory in ViewModel.TemporaryDirectories)
                {
                    try { Directory.Delete(eachDirectory, true); }
                    catch { }
                }
            }
        }
    }
}
