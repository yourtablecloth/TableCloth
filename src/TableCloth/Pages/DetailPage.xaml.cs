using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Navigation;
using TableCloth.Contracts;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Models.WindowsSandbox;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Pages
{
    /// <summary>
    /// Interaction logic for DetailPage.xaml
    /// </summary>
    public partial class DetailPage : Page, IPageArgument<DetailPageModel>
    {
        public DetailPage()
        {
            InitializeComponent();
        }

        public DetailPageViewModel ViewModel
            => (DetailPageViewModel)DataContext;

        public DetailPageModel Arguments { get; set; } = default;

        public CatalogInternetService FirstArgument => Arguments?.SelectedServices?.FirstOrDefault();

        private void RunSandbox(TableClothConfiguration config)
        {
            if (config.CertPair != null)
            {
                var today = DateTime.Now;

                if (today < config.CertPair.NotBefore)
                    ViewModel.AppMessageBox.DisplayError(StringResources.Error_Cert_MayTooEarly(config.CertPair.NotBefore), false);

                if (today > config.CertPair.NotAfter)
                    ViewModel.AppMessageBox.DisplayError(StringResources.Error_Cert_MayExpired(config.CertPair.NotAfter), false);
                else if (today > config.CertPair.NotAfter.AddDays(-3d))
                    ViewModel.AppMessageBox.DisplayInfo(StringResources.Error_Cert_ExpireSoon(config.CertPair.NotAfter));
            }

            var tempPath = ViewModel.SharedLocations.GetTempPath();
            var excludedFolderList = new List<SandboxMappedFolder>();
            var wsbFilePath = ViewModel.SandboxBuilder.GenerateSandboxConfiguration(tempPath, config, excludedFolderList);

            if (excludedFolderList.Any())
                ViewModel.AppMessageBox.DisplayError(StringResources.Error_HostFolder_Unavailable(excludedFolderList.Select(x => x.HostFolder)), false);

            ViewModel.CurrentDirectory = tempPath;
            ViewModel.TemporaryDirectories.Add(tempPath);
            ViewModel.SandboxLauncher.RunSandbox(wsbFilePath);
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            DataContext = new DetailPageViewModel(
                FirstArgument,
                App.Current.Services);

            var currentConfig = ViewModel.PreferencesManager.LoadPreferences();

            if (currentConfig == null)
                currentConfig = ViewModel.PreferencesManager.GetDefaultPreferences();

            ViewModel.EnableLogAutoCollecting = currentConfig.UseLogCollection;
            ViewModel.EnableMicrophone = currentConfig.UseAudioRedirection;
            ViewModel.EnableWebCam = currentConfig.UseVideoRedirection;
            ViewModel.EnablePrinters = currentConfig.UsePrinterRedirection;
            ViewModel.InstallEveryonesPrinter = currentConfig.InstallEveryonesPrinter;
            ViewModel.InstallAdobeReader = currentConfig.InstallAdobeReader;
            ViewModel.InstallHancomOfficeViewer = currentConfig.InstallHancomOfficeViewer;
            ViewModel.InstallRaiDrive = currentConfig.InstallRaiDrive;
            ViewModel.EnableInternetExplorerMode = currentConfig.EnableInternetExplorerMode;
            ViewModel.LastDisclaimerAgreedTime = currentConfig.LastDisclaimerAgreedTime;

            var foundCandidate = ViewModel.CertPairScanner.ScanX509Pairs(ViewModel.CertPairScanner.GetCandidateDirectories()).FirstOrDefault();

            if (foundCandidate != null)
            {
                ViewModel.SelectedCertFile = foundCandidate;
                ViewModel.MapNpkiCert = true;
            }

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            if (ViewModel.ShouldNotifyDisclaimer)
            {
                var disclaimerWindow = new DisclaimerWindow() { Owner = Window.GetWindow(this), };
                var result = disclaimerWindow.ShowDialog();

                if (result.HasValue && result.Value)
                    ViewModel.LastDisclaimerAgreedTime = DateTime.UtcNow;
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var currentConfig = ViewModel.PreferencesManager.LoadPreferences();

            if (currentConfig == null)
                currentConfig = ViewModel.PreferencesManager.GetDefaultPreferences();

            switch (e.PropertyName)
            {
                case nameof(MainWindowViewModel.EnableLogAutoCollecting):
                    currentConfig.UseLogCollection = ViewModel.EnableLogAutoCollecting;
                    if (ViewModel.AppRestartManager.AskRestart())
                    {
                        ViewModel.AppRestartManager.ReserveRestart = true;
                        Window.GetWindow(this).Close();
                    }
                    break;

                case nameof(MainWindowViewModel.EnableMicrophone):
                    currentConfig.UseAudioRedirection = ViewModel.EnableMicrophone;
                    break;

                case nameof(MainWindowViewModel.EnableWebCam):
                    currentConfig.UseVideoRedirection = ViewModel.EnableWebCam;
                    break;

                case nameof(MainWindowViewModel.EnablePrinters):
                    currentConfig.UsePrinterRedirection = ViewModel.EnablePrinters;
                    break;

                case nameof(MainWindowViewModel.InstallEveryonesPrinter):
                    currentConfig.InstallEveryonesPrinter = ViewModel.InstallEveryonesPrinter;
                    break;

                case nameof(MainWindowViewModel.InstallAdobeReader):
                    currentConfig.InstallAdobeReader = ViewModel.InstallAdobeReader;
                    break;

                case nameof(MainWindowViewModel.InstallHancomOfficeViewer):
                    currentConfig.InstallHancomOfficeViewer = ViewModel.InstallHancomOfficeViewer;
                    break;

                case nameof(MainWindowViewModel.InstallRaiDrive):
                    currentConfig.InstallRaiDrive = ViewModel.InstallRaiDrive;
                    break;

                case nameof(MainWindowViewModel.EnableInternetExplorerMode):
                    currentConfig.EnableInternetExplorerMode = ViewModel.EnableInternetExplorerMode;
                    break;

                case nameof(MainWindowViewModel.LastDisclaimerAgreedTime):
                    currentConfig.LastDisclaimerAgreedTime = ViewModel.LastDisclaimerAgreedTime;
                    break;

                default:
                    return;
            }

            ViewModel.PreferencesManager.SavePreferences(currentConfig);
        }

        private void GoBackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SandboxLauncher.IsSandboxRunning())
            {
                ViewModel.AppMessageBox.DisplayError(StringResources.Error_Windows_Sandbox_Already_Running, false);
                return;
            }

            var selectedCert = ViewModel.SelectedCertFile;

            if (!ViewModel.MapNpkiCert)
                selectedCert = null;

            var config = new TableClothConfiguration()
            {
                CertPair = selectedCert,
                EnableMicrophone = ViewModel.EnableMicrophone,
                EnableWebCam = ViewModel.EnableWebCam,
                EnablePrinters = ViewModel.EnablePrinters,
                InstallEveryonesPrinter = ViewModel.InstallEveryonesPrinter,
                InstallAdobeReader = ViewModel.InstallAdobeReader,
                InstallHancomOfficeViewer = ViewModel.InstallHancomOfficeViewer,
                InstallRaiDrive = ViewModel.InstallRaiDrive,
                EnableInternetExplorerMode = ViewModel.EnableInternetExplorerMode,
                Companions = new CatalogCompanion[] { }, /*ViewModel.CatalogDocument.Companions*/
                Services = new[] { ViewModel.SelectedService, },
            };

            RunSandbox(config);
        }

        private void CreateShortcutButton_Click(object sender, RoutedEventArgs e)
        {
            var options = new List<string>();
            var targetPath = Process.GetCurrentProcess().MainModule.FileName;
            var linkName = StringResources.AppName;

            if (ViewModel.EnableMicrophone)
                options.Add(StringResources.TableCloth_Switch_EnableMicrophone);
            if (ViewModel.EnableWebCam)
                options.Add(StringResources.TableCloth_Switch_EnableCamera);
            if (ViewModel.EnablePrinters)
                options.Add(StringResources.TableCloth_Switch_EnablePrinter);
            if (ViewModel.InstallEveryonesPrinter)
                options.Add(StringResources.TableCloth_Switch_InstallEveryonesPrinter);
            if (ViewModel.InstallAdobeReader)
                options.Add(StringResources.TableCloth_Switch_InstallAdobeReader);
            if (ViewModel.InstallHancomOfficeViewer)
                options.Add(StringResources.TableCloth_Switch_InstallHancomOfficeViewer);
            if (ViewModel.InstallRaiDrive)
                options.Add(StringResources.TableCloth_Switch_InstallRaiDrive);
            if (ViewModel.EnableInternetExplorerMode)
                options.Add(StringResources.TableCloth_Switch_EnableIEMode);
            if (ViewModel.MapNpkiCert)
                options.Add(StringResources.Tablecloth_Switch_EnableCert);

            var firstSite = ViewModel.SelectedService;
            var iconFilePath = default(string);

            if (firstSite != null)
            {
                options.Add(firstSite.Id);
                linkName = firstSite.DisplayName;

                iconFilePath = Path.Combine(
                    ViewModel.SharedLocations.GetImageDirectoryPath(),
                    $"{firstSite.Id}.ico");

                if (!File.Exists(iconFilePath))
                    iconFilePath = null;
            }

            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var fullPath = Path.Combine(desktopPath, linkName + ".lnk");

            for (int i = 1; File.Exists(fullPath); ++i)
                fullPath = Path.Combine(desktopPath, linkName + $" ({i}).lnk");

            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(fullPath);
                shortcut.TargetPath = targetPath;

                if (iconFilePath != null && File.Exists(iconFilePath))
                    shortcut.IconLocation = iconFilePath;

                shortcut.Arguments = String.Join(' ', options.ToArray());
                shortcut.Save();
            }
            catch
            {
                ViewModel.AppMessageBox.DisplayInfo(StringResources.Error_ShortcutFailed);
                return;
            }

            ViewModel.AppMessageBox.DisplayInfo(StringResources.Info_ShortcutSuccess);
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var certSelectWindow = new CertSelectWindow() { Owner = Window.GetWindow(this) };
            var response = certSelectWindow.ShowDialog();

            if (!response.HasValue || !response.Value)
                return;

            if (certSelectWindow.ViewModel.SelectedCertPair != null)
                ViewModel.SelectedCertFile = certSelectWindow.ViewModel.SelectedCertPair;
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Hyperlink link)
                return;

            if (!Uri.TryCreate(link.Tag?.ToString(), UriKind.Absolute, out Uri uri) ||
                uri == null)
                return;

            Process.Start(new ProcessStartInfo(uri.ToString())
            {
                UseShellExecute = true,
            });
        }

        private void SiteCatalogFilter_LostFocus(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(
                new Uri("Pages/CatalogPage.xaml", UriKind.Relative),
                new CatalogPageModel(SiteCatalogFilter.Text));
        }
    }
}
