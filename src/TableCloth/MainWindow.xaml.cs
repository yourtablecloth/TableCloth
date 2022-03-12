using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using TableCloth.Implementations.WindowsSandbox;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Resources;
using TableCloth.Themes;
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
        private bool _requireRestart = false;

        private static bool? IsLightThemeApplied()
        {
            // https://stackoverflow.com/questions/51334674/how-to-detect-windows-10-light-dark-mode-in-win32-application
            using (var personalizeKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false))
            {
                if (personalizeKey != null)
                {
                    if (personalizeKey.GetValueKind("AppsUseLightTheme") == RegistryValueKind.DWord)
                    {
                        return (int)personalizeKey.GetValue("AppsUseLightTheme", 1) > 0;
                    }
                }
            }

            return null;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // https://stackoverflow.com/questions/51334674/how-to-detect-windows-10-light-dark-mode-in-win32-application
            const int WM_SETTINGCHANGE = 0x001A;

            if (msg == WM_SETTINGCHANGE)
            {
                var data = Marshal.PtrToStringAuto(lParam);
                if (string.Equals(data, "ImmersiveColorSet", StringComparison.Ordinal))
                {
                    var appliedLightTheme = IsLightThemeApplied();
                    if (appliedLightTheme.HasValue)
                    {
                        if (appliedLightTheme.Value)
                            ThemesController.CurrentTheme = ThemeTypes.ColourfulLight;
                        else
                            ThemesController.CurrentTheme = ThemeTypes.ColourfulDark;
                        handled = true;
                    }
                }
            }

            return IntPtr.Zero;
        }

        private static async void LoadSiteImages(List<CatalogInternetService> services, string imageDirectoryPath)
        {
            if (!Directory.Exists(imageDirectoryPath))
                Directory.CreateDirectory(imageDirectoryPath);

            var httpClient = Shared.HttpClientFactory.Value;

            foreach (var eachSite in services)
            {
                var targetFilePath = Path.Combine(imageDirectoryPath, eachSite.Id + ".png");

                try
                {
                    if (File.Exists(targetFilePath))
                        continue;

                    var targetUrl = $"{StringResources.ImageUrlPrefix}/{eachSite.Category}/{eachSite.Id}.png";
                    var imageStream = await httpClient.GetStreamAsync(targetUrl);

                    using var fileStream = File.OpenWrite(targetFilePath);
                    await imageStream.CopyToAsync(fileStream);
                }
                catch
                {
                    try { File.WriteAllBytes(targetFilePath, Properties.Resources.SandboxIcon); }
                    catch { }
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //SiteListHelpRow.Height = new GridLength(0d);
            //SiteListSearchRow.Height = new GridLength(0d);
            //SiteListRow.Height = new GridLength(0d);

            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(WndProc);

            var appliedLightTheme = IsLightThemeApplied();
            if (appliedLightTheme.HasValue)
            {
                if (appliedLightTheme.Value)
                    ThemesController.CurrentTheme = ThemeTypes.ColourfulLight;
                else
                    ThemesController.CurrentTheme = ThemeTypes.ColourfulDark;
            }

            var currentConfig = ViewModel.Preferences.LoadConfig();

            if (currentConfig == null)
                currentConfig = ViewModel.Preferences.GetDefaultConfig();

            ViewModel.EnableLogAutoCollecting = currentConfig.UseLogCollection;
            ViewModel.EnableMicrophone = currentConfig.UseAudioRedirection;
            ViewModel.EnableWebCam = currentConfig.UseVideoRedirection;
            ViewModel.EnablePrinters = currentConfig.UsePrinterRedirection;
            ViewModel.EnableEveryonesPrinter = currentConfig.InstallEveryonesPrinter;
            ViewModel.EnableAdobeReader = currentConfig.InstallAdobeReader;
            ViewModel.EnableHancomOfficeViewer = currentConfig.InstallHancomOfficeViewer;
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
                var disclaimerWindow = new DisclaimerWindow();
                var result = disclaimerWindow.ShowDialog();

                if (result.HasValue && result.Value)
                    ViewModel.LastDisclaimerAgreedTime = DateTime.UtcNow;
            }

            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(SiteCatalog.ItemsSource);
            view.Filter = SiteCatalog_Filter;

            var services = ViewModel.Services;
            var directoryPath = ViewModel.SharedLocations.GetImageDirectoryPath();
            Task.Factory.StartNew(() => LoadSiteImages(services, directoryPath));
        }

        private bool SiteCatalog_Filter(object item)
        {
            var filterText = SiteCatalogFilter.Text;

            if (string.IsNullOrWhiteSpace(filterText))
                return true;

            var actualItem = item as CatalogInternetService;

            if (actualItem == null)
                return true;

            var splittedFilterText = filterText.Split(new char[] { ',', }, StringSplitOptions.RemoveEmptyEntries);
            var result = false;

            foreach (var eachFilterText in splittedFilterText)
            {
                result |= actualItem.DisplayName.Contains(eachFilterText, StringComparison.OrdinalIgnoreCase)
                    || actualItem.CategoryDisplayName.Contains(eachFilterText, StringComparison.OrdinalIgnoreCase)
                    || actualItem.Url.Contains(eachFilterText, StringComparison.OrdinalIgnoreCase)
                    || actualItem.Packages.Count.ToString().Contains(eachFilterText, StringComparison.OrdinalIgnoreCase)
                    || actualItem.Packages.Any(x => x.Name.Contains(eachFilterText, StringComparison.OrdinalIgnoreCase))
                    || actualItem.Id.Contains(eachFilterText, StringComparison.OrdinalIgnoreCase);
            }

            return result;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var currentConfig = ViewModel.Preferences.LoadConfig();

            if (currentConfig == null)
                currentConfig = ViewModel.Preferences.GetDefaultConfig();

            switch (e.PropertyName)
            {
                case nameof(MainWindowViewModel.EnableLogAutoCollecting):
                    currentConfig.UseLogCollection = ViewModel.EnableLogAutoCollecting;
                    if (ViewModel.AppMessageBox.DisplayInfo(this, StringResources.Ask_RestartRequired, MessageBoxButton.OKCancel).Equals(MessageBoxResult.OK))
                    {
                        _requireRestart = true;
                        Close();
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

                case nameof(MainWindowViewModel.EnableEveryonesPrinter):
                    currentConfig.InstallEveryonesPrinter = ViewModel.EnableEveryonesPrinter;
                    break;

                case nameof(MainWindowViewModel.EnableAdobeReader):
                    currentConfig.InstallAdobeReader = ViewModel.EnableAdobeReader;
                    break;

                case nameof(MainWindowViewModel.EnableHancomOfficeViewer):
                    currentConfig.InstallHancomOfficeViewer = ViewModel.EnableHancomOfficeViewer;
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

            ViewModel.Preferences.SaveConfig(currentConfig);
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var certSelectWindow = new CertSelectWindow();
            var response = certSelectWindow.ShowDialog();

            if (!response.HasValue || !response.Value)
                return;

            if (certSelectWindow.ViewModel.SelectedCertPair != null)
                ViewModel.SelectedCertFile = certSelectWindow.ViewModel.SelectedCertPair;
        }

        private void SiteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = (ListBox)e.Source;
            _selectedSites = listBox.SelectedItems.Cast<CatalogInternetService>().ToList();
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var aboutWindow = new AboutWindow();
            aboutWindow.ShowDialog();
        }

        private void LaunchSandboxButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SandboxLauncher.IsSandboxRunning())
            {
                ViewModel.AppMessageBox.DisplayError(this, StringResources.Error_Windows_Sandbox_Already_Running, false);
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
                EnableEveryonesPrinter = ViewModel.EnableEveryonesPrinter,
                EnableAdobeReader = ViewModel.EnableAdobeReader,
                EnableHancomOfficeViewer = ViewModel.EnableHancomOfficeViewer,
                EnableInternetExplorerMode = ViewModel.EnableInternetExplorerMode,
                Companions = ViewModel.CatalogDocument.Companions,
                Services = _selectedSites,
            };

            var tempPath = ViewModel.SharedLocations.GetTempPath();
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

            if (_requireRestart)
            {
                var filePath = Process.GetCurrentProcess().MainModule.FileName;
                var arguments = Environment.GetCommandLineArgs().Skip(1).ToArray();
                Process.Start(filePath, arguments);
            }
        }

        private void AgreeDisclaimer_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.LastDisclaimerAgreedTime = DateTime.UtcNow;
        }

        private void SiteCatalogFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(SiteCatalog.ItemsSource).Refresh();
        }

        // https://stackoverflow.com/questions/660554/how-to-automatically-select-all-text-on-focus-in-wpf-textbox

        private void SiteCatalogFilter_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Fixes issue when clicking cut/copy/paste in context menu
            if (SiteCatalogFilter.SelectionLength < 1)
                SiteCatalogFilter.SelectAll();
        }

        private void SiteCatalogFilter_LostMouseCapture(object sender, MouseEventArgs e)
        {
            // If user highlights some text, don't override it
            if (SiteCatalogFilter.SelectionLength < 1)
                SiteCatalogFilter.SelectAll();

            // further clicks will not select all
            SiteCatalogFilter.LostMouseCapture -= SiteCatalogFilter_LostMouseCapture;
        }

        private void SiteCatalogFilter_LostTouchCapture(object sender, TouchEventArgs e)
        {
            // If user highlights some text, don't override it
            if (SiteCatalogFilter.SelectionLength < 1)
                SiteCatalogFilter.SelectAll();

            // further clicks will not select all
            SiteCatalogFilter.LostTouchCapture -= SiteCatalogFilter_LostTouchCapture;
        }

        private void SiteCatalogFilter_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // once we've left the TextBox, return the select all behavior
            SiteCatalogFilter.LostMouseCapture += SiteCatalogFilter_LostMouseCapture;
            SiteCatalogFilter.LostTouchCapture += SiteCatalogFilter_LostTouchCapture;
        }

        private void ReloadCatalogButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(
                Process.GetCurrentProcess().MainModule.FileName,
                Environment.GetCommandLineArgs().Skip(1).ToArray());
            Application.Current.Shutdown();
        }
    }
}
