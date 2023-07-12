using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Models.WindowsSandbox;
using TableCloth.Resources;
using TableCloth.Themes;
using TableCloth.ViewModels;

namespace TableCloth
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
            if (msg == NativeMethods.WM_SETTINGCHANGE)
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

        private void RunSandbox(TableClothConfiguration config)
        {
            var tempPath = ViewModel.SharedLocations.GetTempPath();
            var excludedFolderList = new List<SandboxMappedFolder>();
            var wsbFilePath = ViewModel.SandboxBuilder.GenerateSandboxConfiguration(tempPath, config, excludedFolderList);

            if (excludedFolderList.Any())
                ViewModel.AppMessageBox.DisplayError(StringResources.Error_HostFolder_Unavailable(excludedFolderList.Select(x => x.HostFolder)), false);

            ViewModel.CurrentDirectory = tempPath;
            ViewModel.TemporaryDirectories.Add(tempPath);
            ViewModel.SandboxLauncher.RunSandbox(wsbFilePath);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
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
                var disclaimerWindow = new DisclaimerWindow() { Owner = this };
                var result = disclaimerWindow.ShowDialog();

                if (result.HasValue && result.Value)
                    ViewModel.LastDisclaimerAgreedTime = DateTime.UtcNow;
            }

            CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(SiteCatalog.ItemsSource);
            view.Filter = SiteCatalog_Filter;

            var services = ViewModel.Services;
            var directoryPath = ViewModel.SharedLocations.GetImageDirectoryPath();

            await ViewModel.ResourceResolver.LoadSiteImages(
                App.Current.Services.GetService<IHttpClientFactory>(),
                services, directoryPath).ConfigureAwait(false);

            var args = App.Current.Arguments.ToArray();
            var config = new TableClothConfiguration();

            var selectedServices = new List<string>();
            var enableCert = false;
            var certPrivateKeyPath = default(string);
            var certPublicKeyPath = default(string);
            var showHelp = false;

            for (var i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith(StringResources.TableCloth_Switch_Prefix))
                    selectedServices.Add(args[i]);
                else if (string.Equals(args[i], StringResources.TableCloth_Switch_EnableMicrophone, StringComparison.OrdinalIgnoreCase))
                    config.EnableMicrophone = true;
                else if (string.Equals(args[i], StringResources.TableCloth_Switch_EnableCamera, StringComparison.OrdinalIgnoreCase))
                    config.EnableWebCam = true;
                else if (string.Equals(args[i], StringResources.TableCloth_Switch_EnablePrinter, StringComparison.OrdinalIgnoreCase))
                    config.EnablePrinters = true;
                else if (string.Equals(args[i], StringResources.TableCloth_Switch_CertPrivateKey, StringComparison.OrdinalIgnoreCase))
                    certPrivateKeyPath = args[Math.Min(args.Length - 1, ++i)];
                else if (string.Equals(args[i], StringResources.TableCloth_Switch_CertPublicKey, StringComparison.OrdinalIgnoreCase))
                    certPublicKeyPath = args[Math.Min(args.Length - 1, ++i)];
                else if (string.Equals(args[i], StringResources.TableCloth_Switch_InstallEveryonesPrinter, StringComparison.OrdinalIgnoreCase))
                    config.InstallEveryonesPrinter = true;
                else if (string.Equals(args[i], StringResources.TableCloth_Switch_InstallAdobeReader, StringComparison.OrdinalIgnoreCase))
                    config.InstallAdobeReader = true;
                else if (string.Equals(args[i], StringResources.TableCloth_Switch_InstallHancomOfficeViewer, StringComparison.OrdinalIgnoreCase))
                    config.InstallHancomOfficeViewer = true;
                else if (string.Equals(args[i], StringResources.TableCloth_Switch_InstallRaiDrive, StringComparison.OrdinalIgnoreCase))
                    config.InstallRaiDrive = true;
                else if (string.Equals(args[i], StringResources.TableCloth_Switch_EnableIEMode, StringComparison.OrdinalIgnoreCase))
                    config.EnableInternetExplorerMode = true;
                else if (string.Equals(args[i], StringResources.TableCloth_Switch_Help, StringComparison.OrdinalIgnoreCase))
                    showHelp = true;
                else if (string.Equals(args[i], StringResources.Tablecloth_Switch_EnableCert, StringComparison.OrdinalIgnoreCase))
                    enableCert = true;
            }

            if (showHelp)
            {
                ViewModel.AppMessageBox.DisplayInfo(StringResources.TableCloth_TableCloth_Switches_Help, MessageBoxButton.OK);
                return;
            }

            config.Services = services.Where(x => selectedServices.Contains(x.Id, StringComparer.OrdinalIgnoreCase)).ToList();

            if (config.Services.Count > 0)
            {
                if (enableCert)
                {
                    var certPublicKeyData = default(byte[]);
                    var certPrivateKeyData = default(byte[]);

                    if (File.Exists(certPublicKeyPath))
                        certPublicKeyData = File.ReadAllBytes(certPublicKeyPath);

                    if (File.Exists(certPrivateKeyPath))
                        certPrivateKeyData = File.ReadAllBytes(certPrivateKeyPath);

                    if (certPublicKeyData != null && certPrivateKeyData != null)
                        config.CertPair = new X509CertPair(certPublicKeyData, certPrivateKeyData);
                    else
                        config.CertPair = ViewModel.SelectedCertFile;

                }
                RunSandbox(config);
            }
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
            var currentConfig = ViewModel.PreferencesManager.LoadPreferences();

            if (currentConfig == null)
                currentConfig = ViewModel.PreferencesManager.GetDefaultPreferences();

            switch (e.PropertyName)
            {
                case nameof(MainWindowViewModel.EnableLogAutoCollecting):
                    currentConfig.UseLogCollection = ViewModel.EnableLogAutoCollecting;
                    if (ViewModel.AppMessageBox.DisplayInfo(StringResources.Ask_RestartRequired, MessageBoxButton.OKCancel).Equals(MessageBoxResult.OK))
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

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var certSelectWindow = new CertSelectWindow() { Owner = this };
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
            var aboutWindow = new AboutWindow() { Owner = this };
            aboutWindow.ShowDialog();
        }

        private void LaunchSandboxButton_Click(object sender, RoutedEventArgs e)
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
                Companions = ViewModel.CatalogDocument.Companions,
                Services = _selectedSites,
            };

            RunSandbox(config);
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
                var arguments = App.Current.Arguments;
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
                App.Current.Arguments);
            Application.Current.Shutdown();
        }

        private void ShortcutButton_Click(object sender, RoutedEventArgs e)
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

            var firstSite = _selectedSites.FirstOrDefault();
            var iconFilePath = default(string);

            if (firstSite != null)
            {
                options.Add(firstSite.Id);
                linkName = firstSite.DisplayName;

                if (_selectedSites.Count > 1)
                    linkName += StringResources.LinkNamePostfix_ManyOthers(_selectedSites.Count);

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

        #region Sort Support

        GridViewColumnHeader _lastHeaderClicked = null;
        ListSortDirection? _lastDirection = null;

        void GridViewColumnHeaderClickedHandler(object sender,
                                                RoutedEventArgs e)
        {
            var headerClicked = e.OriginalSource as GridViewColumnHeader;
            ListSortDirection? direction;

            if (headerClicked != null)
            {
                if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
                {
                    if (headerClicked != _lastHeaderClicked)
                    {
                        direction = ListSortDirection.Ascending;
                    }
                    else
                    {
                        if (_lastDirection == ListSortDirection.Ascending)
                        {
                            direction = ListSortDirection.Descending;
                        }
                        else if (_lastDirection == null)
                        {
                            direction = ListSortDirection.Ascending;
                        }
                        else
                        {
                            direction = null;
                        }
                    }

                    //var columnBinding = headerClicked.Column.DisplayMemberBinding as Binding;
                    //var sortBy = columnBinding?.Path.Path ?? headerClicked.Column.Header as string;
                    var column = headerClicked.Column as ExtendedGridViewColumn;
                    var sortBy = column?.BindingPath ?? headerClicked.Column.Header as string;

                    Sort(sortBy, direction);

                    if (direction == ListSortDirection.Ascending)
                    {
                        headerClicked.Column.HeaderTemplate =
                          Resources["HeaderTemplateArrowUp"] as DataTemplate;
                    }
                    else if (direction == ListSortDirection.Descending)
                    {
                        headerClicked.Column.HeaderTemplate =
                          Resources["HeaderTemplateArrowDown"] as DataTemplate;
                    }
                    else
                    {
                        headerClicked.Column.HeaderTemplate = null;
                    }

                    // Remove arrow from previously sorted header
                    if (_lastHeaderClicked != null && _lastHeaderClicked != headerClicked)
                    {
                        _lastHeaderClicked.Column.HeaderTemplate = null;
                    }

                    _lastHeaderClicked = headerClicked;
                    _lastDirection = direction;
                }
            }
        }
        private void Sort(string sortBy, ListSortDirection? direction)
        {
            ICollectionView dataView =
              CollectionViewSource.GetDefaultView(SiteCatalog.ItemsSource);

            dataView.SortDescriptions.Clear();
            if (direction != null)
            {
                SortDescription sd = new SortDescription(sortBy, direction ?? ListSortDirection.Ascending);
                dataView.SortDescriptions.Add(sd);
            }
            dataView.Refresh();
        }

        #endregion Sort Support
    }
}
