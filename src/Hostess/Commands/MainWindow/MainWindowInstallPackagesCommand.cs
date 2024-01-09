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
            IResourceCacheManager resourceCacheManager,
            IAppMessageBox appMessageBox,
            ISharedLocations sharedLocations,
            ICommandLineArguments commandLineArguments)
        {
            _resourceCacheManager = resourceCacheManager;
            _appMessageBox = appMessageBox;
            _sharedLocations = sharedLocations;
            _commandLineArguments = commandLineArguments;
        }

        private readonly IResourceCacheManager _resourceCacheManager;
        private readonly IAppMessageBox _appMessageBox;
        private readonly ISharedLocations _sharedLocations;
        private readonly ICommandLineArguments _commandLineArguments;

        private bool _isRunning = false;

        protected override bool EvaluateCanExecute()
            => !_isRunning;

        public override async void Execute(MainWindowViewModel viewModel)
        {
            try
            {
                var parsedArgs = _commandLineArguments.Current;

                _isRunning = true;
                var hasAnyFailure = false;

                var downloadFolderPath = NativeMethods.GetKnownFolderPath(NativeMethods.DownloadFolderGuid);

                if (!Directory.Exists(downloadFolderPath))
                    Directory.CreateDirectory(downloadFolderPath);

                var catalog = _resourceCacheManager.CatalogDocument;

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
                        eachItem.StatusMessage = HostessStrings.Hostess_Install_Failed;
                        eachItem.Installed = false;
                        eachItem.ErrorMessage = ex is AggregateException exception ? exception.InnerException.Message : ex.Message;
                        await Task.Delay(100);
                    }
                }

                if (!hasAnyFailure)
                {
                    if (parsedArgs.InstallEveryonesPrinter ?? false)
                        TryInstallEveryonesPrinter();

                    if (parsedArgs.InstallAdobeReader ?? false)
                        TryInstallAdobeReader();

                    if (parsedArgs.InstallHancomOfficeViewer ?? false)
                        TryInstallHancomOfficeViewer();

                    if (parsedArgs.InstallRaiDrive ?? false)
                        TryInstallRaiDrive();

                    var targets = parsedArgs.SelectedServices;
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
            var parsedArgs = _commandLineArguments.Current;

            if (parsedArgs.DryRun)
                return;

            if (parsedArgs.EnableInternetExplorerMode ?? false)
            {
                try
                {
                    // HKLM\SOFTWARE\Policies\Microsoft\Edge > InternetExplorerIntegrationLevel (REG_DWORD) with value 1, InternetExplorerIntegrationSiteList (REG_SZ)
                    using (var ieModeKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Edge", true))
                    {
                        ieModeKey.SetValue("InternetExplorerIntegrationLevel", 1, RegistryValueKind.DWord);
                        ieModeKey.SetValue("InternetExplorerIntegrationSiteList", ConstantStrings.IEModePolicyXmlUrl, RegistryValueKind.String);
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
            var parsedArgs = _commandLineArguments.Current;

            eachItem.Installed = null;
            eachItem.StatusMessage = HostessStrings.Hostess_Download_InProgress;

            var tempFileName = $"installer_{Guid.NewGuid():n}.exe";
            var tempFilePath = System.IO.Path.Combine(downloadFolderPath, tempFileName);

            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);

            using (var webClient = new WebClient())
            {
                webClient.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml");
                webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Trident/7.0; rv:11.0) like Gecko");
                await webClient.DownloadFileTaskAsync(eachItem.PackageUrl, tempFilePath).ConfigureAwait(false);

                eachItem.StatusMessage = HostessStrings.Hostess_Install_InProgress;

                if (parsedArgs.DryRun)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1d)).ConfigureAwait(false);
                    eachItem.StatusMessage = HostessStrings.Hostess_Install_Succeed;
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
                    eachItem.StatusMessage = HostessStrings.Hostess_Install_Succeed;
                    eachItem.Installed = true;
                    eachItem.ErrorMessage = null;
                }
            }
        }

        private async Task ProcessPowerShellScript(InstallItemViewModel eachItem, string downloadFolderPath)
        {
            var parsedArgs = _commandLineArguments.Current;

            eachItem.Installed = null;
            eachItem.StatusMessage = HostessStrings.Hostess_Install_InProgress;

            var tempFileName = $"bootstrap_{Guid.NewGuid():n}.ps1";
            var tempFilePath = System.IO.Path.Combine(downloadFolderPath, tempFileName);

            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);

            File.WriteAllText(tempFilePath, eachItem.ScriptContent, Encoding.Unicode);
            var powershellPath = _sharedLocations.GetDefaultPowerShellExecutableFilePath();

            if (!File.Exists(powershellPath))
                throw new Exception(HostessStrings.Hostess_No_PowerShell_Error);

            if (parsedArgs.DryRun)
            {
                await Task.Delay(TimeSpan.FromSeconds(1d)).ConfigureAwait(false);
                eachItem.StatusMessage = HostessStrings.Hostess_Install_Succeed;
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
                eachItem.StatusMessage = HostessStrings.Hostess_Install_Succeed;
                eachItem.Installed = true;
                eachItem.ErrorMessage = null;
            }
        }

        private void TryInstallEveryonesPrinter()
        {
            var parsedArgs = _commandLineArguments.Current;

            if (parsedArgs.DryRun)
                return;

            Process.Start(new ProcessStartInfo(CommonStrings.EveryonesPrinterUrl)
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Maximized,
            });
        }

        private void TryInstallAdobeReader()
        {
            var parsedArgs = _commandLineArguments.Current;

            if (parsedArgs.DryRun)
                return;

            Process.Start(new ProcessStartInfo(CommonStrings.AdobeReaderUrl)
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Maximized,
            });
        }

        private void TryInstallHancomOfficeViewer()
        {
            var parsedArgs = _commandLineArguments.Current;

            if (parsedArgs.DryRun)
                return;

            Process.Start(new ProcessStartInfo(CommonStrings.HancomOfficeViewerUrl)
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Maximized,
            });
        }

        private void TryInstallRaiDrive()
        {
            var parsedArgs = _commandLineArguments.Current;

            if (parsedArgs.DryRun)
                return;

            Process.Start(new ProcessStartInfo(CommonStrings.RaiDriveUrl)
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
