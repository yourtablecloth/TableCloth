using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using TableCloth.Components;
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
    public partial class DetailPage : Page, IPageArgument<DetailPageArgumentModel>
    {
        public DetailPage()
        {
            InitializeComponent();
        }

        public DetailPageViewModel ViewModel
            => (DetailPageViewModel)DataContext;

        public DetailPageArgumentModel Arguments { get; set; } = default;

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectedService = Arguments.SelectedService;

            var currentConfig = ViewModel.PreferencesManager.LoadPreferences();

            if (currentConfig == null)
                currentConfig = ViewModel.PreferencesManager.GetDefaultPreferences();

            ViewModel.EnableLogAutoCollecting = currentConfig.UseLogCollection;
            ViewModel.V2UIOptIn = currentConfig.V2UIOptIn;
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

            if (Arguments.BuiltFromCommandLine &&
                Arguments.SelectedService != null)
                ViewModel.LaunchSandboxCommand.Execute(Arguments);

            if (!string.IsNullOrEmpty(Arguments.CurrentSearchString))
                SiteCatalogFilter.Text = Arguments.CurrentSearchString;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var currentConfig = ViewModel.PreferencesManager.LoadPreferences();

            if (currentConfig == null)
                currentConfig = ViewModel.PreferencesManager.GetDefaultPreferences();

            var window = Window.GetWindow(this);

            switch (e.PropertyName)
            {
                case nameof(MainWindowViewModel.EnableLogAutoCollecting):
                    currentConfig.UseLogCollection = ViewModel.EnableLogAutoCollecting;
                    if (ViewModel.AppRestartManager.AskRestart())
                    {
                        ViewModel.AppRestartManager.ReserveRestart = true;
                        window?.Close();
                    }
                    break;

                case nameof(MainWindowViewModel.V2UIOptIn):
                    currentConfig.V2UIOptIn = ViewModel.V2UIOptIn;
                    if (ViewModel.AppRestartManager.AskRestart())
                    {
                        ViewModel.AppRestartManager.ReserveRestart = true;
                        window?.Close();
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
            NavigationService?.Navigate(
                new Uri("Pages/CatalogPage.xaml", UriKind.Relative),
                new CatalogPageArgumentModel(SiteCatalogFilter.Text));
        }

        // https://stackoverflow.com/questions/660554/how-to-automatically-select-all-text-on-focus-in-wpf-textbox
        private void SiteCatalogFilter_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Fixes issue when clicking cut/copy/paste in context menu
            if (SiteCatalogFilter.SelectionLength < 1)
                SiteCatalogFilter.SelectAll();
        }

        private void SiteCatalogFilter_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape || e.Key == Key.Tab)
            {
                NavigationService.Navigate(
                    new Uri("Pages/CatalogPage.xaml", UriKind.Relative),
                    new CatalogPageArgumentModel(SiteCatalogFilter.Text));
            }
        }
    }
}
