using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using TableCloth.Models.Catalog;
using TableCloth.Resources;
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

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //SiteListHelpRow.Height = new GridLength(0d);
            //SiteListSearchRow.Height = new GridLength(0d);
            //SiteListRow.Height = new GridLength(0d);

            ViewModel.VisualThemeManager.ApplyAutoThemeChange(this);
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

            // Command Line Parse
            var args = App.Current.Arguments.ToArray();

            if (args.Count() > 0)
            {
                var parsedArg = ViewModel.CommandLineParser.ParseForV1(args);

                if (parsedArg.ShowCommandLineHelp)
                {
                    ViewModel.AppMessageBox.DisplayInfo(StringResources.TableCloth_TableCloth_Switches_Help, MessageBoxButton.OK);
                    return;
                }

                if (parsedArg.SelectedServices.Count() > 0)
                    ViewModel.LaunchSandboxCommand.Execute(parsedArg);
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
                        ViewModel.RequireRestart = true;
                        Close();
                    }
                    break;

                case nameof(MainWindowViewModel.V2UIOptIn):
                    currentConfig.V2UIOptIn = ViewModel.V2UIOptIn;
                    if (ViewModel.AppMessageBox.DisplayInfo(StringResources.Ask_RestartRequired, MessageBoxButton.OKCancel).Equals(MessageBoxResult.OK))
                    {
                        ViewModel.RequireRestart = true;
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
            ViewModel.SelectedServices = listBox.SelectedItems.Cast<CatalogInternetService>().ToList();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            ViewModel.SandboxCleanupManager.TryCleanup();

            if (ViewModel.RequireRestart)
                ViewModel.AppRestartManager.RestartNow();
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

        private void SiteCatalogFilter_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // once we've left the TextBox, return the select all behavior
            SiteCatalogFilter.LostMouseCapture += SiteCatalogFilter_LostMouseCapture;
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
