using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace Hostess
{
    public partial class MainWindow : Window
    {
        public MainWindow()
            : base()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Width = MinWidth;
            Height = SystemParameters.PrimaryScreenHeight * 0.5;
            Top = SystemParameters.PrimaryScreenHeight / 2 - Height / 2;
            Left = SystemParameters.PrimaryScreenWidth - Width;

            CatalogDocument catalog = Application.Current.GetCatalogDocument();
            IEnumerable<string> targets = Application.Current.GetInstallSites();
            var packages = new List<InstallItemViewModel>();

            foreach (string eachTargetName in targets)
            {
                CatalogInternetService targetService = catalog.Services.FirstOrDefault(x => string.Equals(eachTargetName, x.Id, StringComparison.Ordinal));

                if (targetService == null)
                {
                    continue;
                }

                packages.AddRange(targetService.Packages.Select(eachPackage => new InstallItemViewModel()
                {
                    TargetSiteName = targetService.DisplayName,
                    PackageName = eachPackage.Name,
                    PackageUrl = eachPackage.Url,
                    Arguments = eachPackage.Arguments,
                    Installed = null,
                }));
            }

            InstallList.ItemsSource = new ObservableCollection<InstallItemViewModel>(packages);

            if (catalog.Services.Where(x => targets.Contains(x.Id)).Any(x => !string.IsNullOrWhiteSpace(x.CompatibilityNotes)))
            {
                PrecautionsWindow window = new PrecautionsWindow();
                window.ShowDialog();
            }
            else
            {
                var peer = new ButtonAutomationPeer(PerformInstallButton);
                var invokeProv = peer.GetPattern(PatternInterface.Invoke) as IInvokeProvider;
                invokeProv.Invoke();
            }
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            _ = MessageBox.Show(this,
                StringResources.AboutDialog_BodyText, StringResources.AppName,
                MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK);
        }

        private async void PerformInstallButton_Click(object sender, RoutedEventArgs e)
        {
            using (var webClient = new WebClient())
            {
                try
                {
                    PerformInstallButton.IsEnabled = false;
                    var hasAnyFailure = false;

                    foreach (InstallItemViewModel eachItem in InstallList.ItemsSource)
                    {
                        try
                        {
                            eachItem.Installed = null;
                            eachItem.StatusMessage = StringResources.Hostess_Download_InProgress;

                            var tempFileName = $"installer_{Guid.NewGuid():n}.exe";
                            var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);

                            if (File.Exists(tempFilePath))
                                File.Delete(tempFilePath);
                            await webClient.DownloadFileTaskAsync(eachItem.PackageUrl, tempFilePath);

                            eachItem.StatusMessage = StringResources.Hostess_Install_InProgress;
                            var psi = new ProcessStartInfo(tempFilePath, eachItem.Arguments)
                            {
                                UseShellExecute = false,
                            };

                            var cpSource = new TaskCompletionSource<int>();
                            using (var process = new Process() { StartInfo = psi, })
                            {
                                process.EnableRaisingEvents = true;
                                process.Exited += (_sender, _e) =>
                                {
                                    var realSender = _sender as Process;
                                    cpSource.SetResult(realSender.ExitCode);
                                };

                                if (!process.Start())
                                    throw new ApplicationException(StringResources.HostessError_Package_CanNotStart);

                                _ = await cpSource.Task;
                                eachItem.StatusMessage = StringResources.Hostess_Install_Succeed;
                                eachItem.Installed = true;
                                eachItem.ErrorMessage = null;
                            }
                        }
                        catch (Exception ex)
                        {
                            hasAnyFailure = true;
                            eachItem.StatusMessage = StringResources.Hostess_Install_Failed;
                            eachItem.Installed = false;
                            eachItem.ErrorMessage = ex is AggregateException exception ? exception.InnerException.Message : ex.Message;
                            await Task.Delay(100);
                        }
                    }

                    if (!hasAnyFailure)
                    {
                        Close();
                        return;
                    }
                }
                finally
                {
                    PerformInstallButton.IsEnabled = true;
                }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            CatalogDocument catalog = Application.Current.GetCatalogDocument();
            IEnumerable<string> targets = Application.Current.GetInstallSites();

            foreach (var eachUrl in catalog.Services.Where(x => targets.Contains(x.Id)).Select(x => x.Url))
            {
                var psi = new ProcessStartInfo(eachUrl) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Maximized, };
                Process.Start(psi);
            }
        }
    }
}
