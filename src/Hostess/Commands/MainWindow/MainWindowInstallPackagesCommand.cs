using Hostess.Components;
using Hostess.ViewModels;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TableCloth;
using TableCloth.Resources;

namespace Hostess.Commands.MainWindow
{
    public sealed class MainWindowInstallPackagesCommand : ViewModelCommandBase<MainWindowViewModel>
    {
        public MainWindowInstallPackagesCommand(
            SharedProperties sharedProperties,
            AppMessageBox appMessageBox,
            SharedLocations sharedLocations)
        {
            _sharedProperties = sharedProperties;
            _appMessageBox = appMessageBox;
            _sharedLocations = sharedLocations;
        }

        private readonly SharedProperties _sharedProperties;
        private readonly AppMessageBox _appMessageBox;
        private readonly SharedLocations _sharedLocations;

        private bool _isRunning = false;

        protected override bool EvaluateCanExecute()
            => !_isRunning;

        public override async void Execute(MainWindowViewModel viewModel)
        {
            try
            {
                _isRunning = true;
                var hasAnyFailure = false;

                var downloadFolderPath = NativeMethods.GetKnownFolderPath(NativeMethods.DownloadFolderGuid);

                if (!Directory.Exists(downloadFolderPath))
                    Directory.CreateDirectory(downloadFolderPath);

                var catalog = _sharedProperties.GetCatalogDocument();

                await EnableIEModeAsync();

                foreach (InstallItemViewModel eachItem in viewModel.InstallItems)
                {
                    try
                    {
                        if (eachItem.InstallItemType == InstallItemType.DownloadAndInstall)
                            await ProcessDownloadAndInstall(eachItem, downloadFolderPath);
                        else if (eachItem.InstallItemType == InstallItemType.PowerShellScript)
                            await ProcessPowerShellScript(eachItem, downloadFolderPath);
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
                    if (_sharedProperties.WillInstallEveryonesPrinter())
                        TryInstallEveryonesPrinter();

                    if (_sharedProperties.WillInstallAdobeReader())
                        TryInstallAdobeReader();

                    if (_sharedProperties.WillInstallHancomOfficeViewer())
                        TryInstallHancomOfficeViewer();

                    if (_sharedProperties.WillInstallRaiDrive())
                        TryInstallRaiDrive();

                    var targets = _sharedProperties.GetInstallSites();
                    var urls = catalog.Services.Where(x => targets.Contains(x.Id)).Select(x => x.Url);
                    TryOpenRequestedWebSites(urls);

                    viewModel.RequestClose(this);
                    return;
                }
            }
            catch (Exception ex)
            {
                _appMessageBox.DisplayError(ex, true);
            }
            finally
            {
                _isRunning = false;
            }
        }

        private async Task EnableIEModeAsync()
        {
            if (_sharedProperties.HasDryRunEnabled())
                return;

            if (_sharedProperties.HasIEModeEnabled())
            {
                try
                {
                    // HKLM\SOFTWARE\Policies\Microsoft\Edge > InternetExplorerIntegrationLevel (REG_DWORD) with value 1, InternetExplorerIntegrationSiteList (REG_SZ)
                    using (var ieModeKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Edge", true))
                    {
                        ieModeKey.SetValue("InternetExplorerIntegrationLevel", 1, RegistryValueKind.DWord);
                        ieModeKey.SetValue("InternetExplorerIntegrationSiteList", StringResources.IEModePolicyXmlUrl, RegistryValueKind.String);
                    }

                    // msedge.exe 파일 경로를 유추하고, Policy를 반영하기 위해 잠시 실행했다가 종료하는 동작을 추가
                    var msedgePath = default(string);

                    if (!_sharedLocations.TryGetMicrosoftEdgeExecutableFilePath(out msedgePath))
                        msedgePath = _sharedLocations.GetDefaultX86MicrosoftEdgeExecutableFilePath();

                    if (File.Exists(msedgePath))
                    {
                        var msedgePsi = new ProcessStartInfo(msedgePath, "about:blank")
                        {
                            UseShellExecute = false,
                            WindowStyle = ProcessWindowStyle.Minimized,
                        };

                        using (var msedgeProcess = Process.Start(msedgePsi))
                        {
                            var tcs = new TaskCompletionSource<int>();
                            msedgeProcess.EnableRaisingEvents = true;
                            msedgeProcess.Exited += (_sender, _e) =>
                            {
                                tcs.SetResult(msedgeProcess.ExitCode);
                            };
                            await Task.Delay(TimeSpan.FromSeconds(1.5d)).ConfigureAwait(false);
                            msedgeProcess.CloseMainWindow();
                            await tcs.Task.ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _appMessageBox.DisplayError(ex, false);
                }
            }
        }

        private async Task ProcessDownloadAndInstall(InstallItemViewModel eachItem, string downloadFolderPath)
        {
            eachItem.Installed = null;
            eachItem.StatusMessage = StringResources.Hostess_Download_InProgress;

            var tempFileName = $"installer_{Guid.NewGuid():n}.exe";
            var tempFilePath = System.IO.Path.Combine(downloadFolderPath, tempFileName);

            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);

            using (var webClient = new WebClient())
            {
                webClient.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml");
                webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Trident/7.0; rv:11.0) like Gecko");
                await webClient.DownloadFileTaskAsync(eachItem.PackageUrl, tempFilePath).ConfigureAwait(false);

                eachItem.StatusMessage = StringResources.Hostess_Install_InProgress;

                if (_sharedProperties.HasDryRunEnabled())
                {
                    await Task.Delay(TimeSpan.FromSeconds(1d)).ConfigureAwait(false);
                    eachItem.StatusMessage = StringResources.Hostess_Install_Succeed;
                    eachItem.Installed = true;
                    eachItem.ErrorMessage = null;
                    return;
                }

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

                    await cpSource.Task.ConfigureAwait(false);
                    eachItem.StatusMessage = StringResources.Hostess_Install_Succeed;
                    eachItem.Installed = true;
                    eachItem.ErrorMessage = null;
                }
            }
        }

        private async Task ProcessPowerShellScript(InstallItemViewModel eachItem, string downloadFolderPath)
        {
            eachItem.Installed = null;
            eachItem.StatusMessage = StringResources.Hostess_Install_InProgress;

            var tempFileName = $"bootstrap_{Guid.NewGuid():n}.ps1";
            var tempFilePath = System.IO.Path.Combine(downloadFolderPath, tempFileName);

            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);

            File.WriteAllText(tempFilePath, eachItem.ScriptContent, Encoding.Unicode);
            var powershellPath = _sharedLocations.GetDefaultPowerShellExecutableFilePath();

            if (!File.Exists(powershellPath))
                throw new Exception(StringResources.Hostess_No_PowerShell_Error);

            if (_sharedProperties.HasDryRunEnabled())
            {
                await Task.Delay(TimeSpan.FromSeconds(1d)).ConfigureAwait(false);
                eachItem.StatusMessage = StringResources.Hostess_Install_Succeed;
                eachItem.Installed = true;
                eachItem.ErrorMessage = null;
                return;
            }

            var psi = new ProcessStartInfo(powershellPath, $"Set-ExecutionPolicy Bypass -Scope Process -Force; {tempFilePath}")
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

                await cpSource.Task.ConfigureAwait(false);
                eachItem.StatusMessage = StringResources.Hostess_Install_Succeed;
                eachItem.Installed = true;
                eachItem.ErrorMessage = null;
            }
        }

        private void TryInstallEveryonesPrinter()
        {
            if (_sharedProperties.HasDryRunEnabled())
                return;

            Process.Start(new ProcessStartInfo(StringResources.EveryonesPrinterUrl)
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Maximized,
            });
        }

        private void TryInstallAdobeReader()
        {
            if (_sharedProperties.HasDryRunEnabled())
                return;

            Process.Start(new ProcessStartInfo(StringResources.AdobeReaderUrl)
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Maximized,
            });
        }

        private void TryInstallHancomOfficeViewer()
        {
            if (_sharedProperties.HasDryRunEnabled())
                return;

            Process.Start(new ProcessStartInfo(StringResources.HancomOfficeViewerUrl)
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Maximized,
            });
        }

        private void TryInstallRaiDrive()
        {
            if (_sharedProperties.HasDryRunEnabled())
                return;

            Process.Start(new ProcessStartInfo(StringResources.RaiDriveUrl)
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Maximized,
            });
        }

        private void TryOpenRequestedWebSites(IEnumerable<string> webSiteUrls)
        {
            foreach (var eachUrl in webSiteUrls)
            {
                Process.Start(new ProcessStartInfo(eachUrl)
                {
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Maximized,
                });
            }
        }
    }
}
