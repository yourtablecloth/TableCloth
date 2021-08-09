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
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private List<CatalogInternetService> _selectedSites = new ();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm)
                return;

            var foundCandidate = vm.CertPairScanner.ScanX509Pairs(vm.CertPairScanner.GetCandidateDirectories()).SingleOrDefault();

            if (foundCandidate != null)
            {
                vm.SelectedCertFiles = new string[]
                {
                    foundCandidate.DerFilePath,
                    foundCandidate.KeyFilePath,
                }.ToList();

                vm.MapNpkiCert = true;
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var certSelectWindow = new CertSelectWindow();
            var response = certSelectWindow.ShowDialog();

            if (!response.HasValue || !response.Value)
                return;

            if (certSelectWindow.DataContext is CertSelectWindowViewModel cvm &&
                cvm.SelectedCertPair != null &&
                File.Exists(cvm.SelectedCertPair.DerFilePath) &&
                File.Exists(cvm.SelectedCertPair.KeyFilePath))
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    vm.SelectedCertFiles = new string[]
                    {
                        cvm.SelectedCertPair.DerFilePath,
                        cvm.SelectedCertPair.KeyFilePath,
                    }.ToList();
                }
            }
        }

        private void SiteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = (ListBox)e.Source;
            _selectedSites = listBox.SelectedItems.Cast<CatalogInternetService>().ToList();
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
                vm.AppMessageBox.DisplayInfo(this, StringResources.AboutDialog_BodyText);
        }

        private void LaunchSandboxButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is MainWindowViewModel vm))
                return;

            var isSandboxRunning = Process.GetProcesses()
                .Where(x => x.ProcessName.StartsWith("WindowsSandbox", StringComparison.OrdinalIgnoreCase))
                .Any();

            if (isSandboxRunning)
            {
                vm.AppMessageBox.DisplayError(this, StringResources.Error_Windows_Sandbox_Already_Running, false);
                return;
            }

            var pair = default(X509CertPair);
            var fileList = vm.SelectedCertFiles;

            if (vm.MapNpkiCert && fileList != null)
            {
                var derFilePath = fileList.Where(x => string.Equals(Path.GetExtension(x), ".der", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                var keyFilePath = fileList.Where(x => string.Equals(Path.GetExtension(x), ".key", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                if (File.Exists(derFilePath) && File.Exists(keyFilePath))
                    pair = vm.CertPairScanner.CreateX509CertPair(derFilePath, keyFilePath);
            }

            var config = new TableClothConfiguration()
            {
                CertPair = pair,
                EnableMicrophone = vm.EnableMicrophone,
                EnableWebCam = vm.EnableWebCam,
                EnablePrinters = vm.EnablePrinters,
                Packages = _selectedSites,
            };

            var tempPath = Path.Combine(vm.AppStartup.AppDataDirectoryPath, $"bwsb_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}");
            var excludedFolderList = new List<SandboxMappedFolder>();
            var wsbFilePath = vm.SandboxBuilder.GenerateSandboxConfiguration(tempPath, config, excludedFolderList);

            if (excludedFolderList.Any())
                vm.AppMessageBox.DisplayError(this, StringResources.Error_HostFolder_Unavailable(excludedFolderList.Select(x => x.HostFolder)), false);

            vm.TemporaryDirectories.Add(tempPath);
            vm.SandboxLauncher.RunSandbox(vm.AppUserInterface, tempPath, wsbFilePath);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
