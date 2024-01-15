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
                _isRunning = true;
                var hasAnyFailure = false;
                var catalog = _resourceCacheManager.CatalogDocument;

                foreach (InstallItemViewModel eachItem in viewModel.InstallItems)
                {
                    try
                    {
                        if (eachItem.InstallItemType == InstallItemType.DownloadAndInstall)
                            await ProcessDownloadAndInstallAsync(eachItem);
                        else if (eachItem.InstallItemType == InstallItemType.PowerShellScript)
                            await ProcessPowerShellScriptAsync(eachItem);
                        else if (eachItem.InstallItemType == InstallItemType.OpenWebSite)
                            await OpenAddInWebSiteAsync(eachItem);
                        else if (eachItem.InstallItemType == InstallItemType.CustomAction)
                            await eachItem.CustomAction?.Invoke(eachItem);

                        eachItem.StatusMessage = UIStringResources.Hostess_Install_Succeed;
                        eachItem.Installed = true;
                        eachItem.ErrorMessage = null;
                    }
                    catch (Exception ex)
                    {
                        hasAnyFailure = true;
                        eachItem.StatusMessage = UIStringResources.Hostess_Install_Failed;
                        eachItem.Installed = false;
                        eachItem.ErrorMessage = ex is AggregateException exception ? exception.InnerException.Message : ex.Message;
                        await Task.Delay(100);
                    }
                }

                if (!hasAnyFailure)
                {
                    var parsedArgs = _commandLineArguments.Current;
                    var targets = parsedArgs.SelectedServices;

                    foreach (var eachUrl in catalog.Services.Where(x => targets.Contains(x.Id)).Select(x => x.Url))
                        await OpenRequestedWebSite(eachUrl);

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

        private async Task ProcessDownloadAndInstallAsync(InstallItemViewModel eachItem)
        {
            var parsedArgs = _commandLineArguments.Current;

            eachItem.Installed = null;
            eachItem.StatusMessage = UIStringResources.Hostess_Download_InProgress;

            var downloadFolderPath = _sharedLocations.GetDownloadDirectoryPath();
            var tempFileName = $"installer_{Guid.NewGuid():n}.exe";
            var tempFilePath = System.IO.Path.Combine(downloadFolderPath, tempFileName);

            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);

            using (var webClient = new WebClient())
            {
                webClient.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml");
                webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Trident/7.0; rv:11.0) like Gecko");
                await webClient.DownloadFileTaskAsync(eachItem.PackageUrl, tempFilePath).ConfigureAwait(false);

                eachItem.StatusMessage = UIStringResources.Hostess_Install_InProgress;

                if (parsedArgs.DryRun)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1d)).ConfigureAwait(false);
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
                        throw new ApplicationException(ErrorStrings.Error_Package_CanNotStart);

                    await cpSource.Task.ConfigureAwait(false);
                }
            }
        }

        private async Task ProcessPowerShellScriptAsync(InstallItemViewModel eachItem)
        {
            var parsedArgs = _commandLineArguments.Current;

            eachItem.Installed = null;
            eachItem.StatusMessage = UIStringResources.Hostess_Install_InProgress;

            var downloadFolderPath = _sharedLocations.GetDownloadDirectoryPath();
            var tempFileName = $"bootstrap_{Guid.NewGuid():n}.ps1";
            var tempFilePath = System.IO.Path.Combine(downloadFolderPath, tempFileName);

            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);

            File.WriteAllText(tempFilePath, eachItem.ScriptContent, Encoding.Unicode);
            var powershellPath = _sharedLocations.GetDefaultPowerShellExecutableFilePath();

            if (!File.Exists(powershellPath))
                throw new Exception(ErrorStrings.Error_No_WindowsPowerShell);

            if (parsedArgs.DryRun)
            {
                await Task.Delay(TimeSpan.FromSeconds(1d)).ConfigureAwait(false);
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
                    throw new ApplicationException(ErrorStrings.Error_Package_CanNotStart);

                await cpSource.Task.ConfigureAwait(false);
            }
        }

        private async Task OpenAddInWebSiteAsync(InstallItemViewModel viewModel)
        {
            var parsedArgs = _commandLineArguments.Current;

            if (parsedArgs.DryRun)
            {
                await Task.Delay(TimeSpan.FromSeconds(1d)).ConfigureAwait(false);
                return;
            }

            Process.Start(new ProcessStartInfo(viewModel.PackageUrl)
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Maximized,
            });
        }

        private async Task OpenRequestedWebSite(string targetUrl)
        {
            var parsedArgs = _commandLineArguments.Current;

            if (parsedArgs.DryRun)
            {
                await Task.Delay(TimeSpan.FromSeconds(1d)).ConfigureAwait(false);
                return;
            }

            Process.Start(new ProcessStartInfo(targetUrl)
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Maximized,
            });
        }
    }
}
