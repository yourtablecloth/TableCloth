using Hostess.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Resources;

namespace Hostess.Components.Implementations
{
    public sealed class StepsPlayer : IStepsPlayer
    {
        public StepsPlayer(
            IResourceCacheManager resourceCacheManager,
            ISharedLocations sharedLocations,
            ICommandLineArguments commandLineArguments,
            IHttpClientFactory httpClientFactory)
        {
            _resourceCacheManager = resourceCacheManager;
            _sharedLocations = sharedLocations;
            _commandLineArguments = commandLineArguments;
            _httpClientFactory = httpClientFactory;
        }

        private readonly IResourceCacheManager _resourceCacheManager;
        private readonly ISharedLocations _sharedLocations;
        private readonly ICommandLineArguments _commandLineArguments;
        private readonly IHttpClientFactory _httpClientFactory;

        public bool IsRunning { get; private set; }

        public async Task<bool> PlayStepsAsync(
            IEnumerable<InstallItemViewModel> composedSteps,
            bool dryRun,
            CancellationToken cancellationToken = default)
        {
            var hasAnyFailure = false;

            IsRunning = true;
            var catalog = _resourceCacheManager.CatalogDocument;

            foreach (var eachItem in composedSteps)
            {
                try
                {
                    if (eachItem.InstallItemType == InstallItemType.DownloadAndInstall)
                        await ProcessDownloadAndInstallAsync(eachItem, cancellationToken).ConfigureAwait(false);
                    else if (eachItem.InstallItemType == InstallItemType.PowerShellScript)
                        await ProcessPowerShellScriptAsync(eachItem, cancellationToken).ConfigureAwait(false);
                    else if (eachItem.InstallItemType == InstallItemType.OpenWebSite)
                        await OpenAddInWebSiteAsync(eachItem, cancellationToken).ConfigureAwait(false);
                    else if (eachItem.InstallItemType == InstallItemType.CustomAction)
                        await eachItem.CustomAction.Invoke(eachItem, cancellationToken).ConfigureAwait(false);

                    eachItem.StatusMessage = UIStringResources.Hostess_Install_Succeed;
                    eachItem.Installed = true;
                    eachItem.ErrorMessage = null;
                }
                catch (Exception ex)
                {
                    hasAnyFailure = true;
                    eachItem.StatusMessage = UIStringResources.Hostess_Install_Failed;
                    eachItem.Installed = false;
                    eachItem.ErrorMessage = ex is AggregateException exception ? exception.InnerException.ToString() : ex.ToString();
                    await Task.Delay(TimeSpan.FromMilliseconds(100d), cancellationToken).ConfigureAwait(false);
                }
            }

            IsRunning = false;

            if (!hasAnyFailure)
            {
                var parsedArgs = _commandLineArguments.Current;
                var targets = parsedArgs.SelectedServices;

                foreach (var eachUrl in catalog.Services.Where(x => targets.Contains(x.Id)).Select(x => x.Url))
                    await OpenRequestedWebSiteAsync(eachUrl, cancellationToken).ConfigureAwait(false);
            }

            return hasAnyFailure;
        }

        private async Task ProcessDownloadAndInstallAsync(InstallItemViewModel eachItem,
            CancellationToken cancellationToken = default)
        {
            var parsedArgs = _commandLineArguments.Current;

            eachItem.Installed = null;
            eachItem.StatusMessage = UIStringResources.Hostess_Download_InProgress;

            var downloadFolderPath = _sharedLocations.GetDownloadDirectoryPath();
            var tempFileName = $"installer_{Guid.NewGuid():n}.exe";
            var tempFilePath = System.IO.Path.Combine(downloadFolderPath, tempFileName);

            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);

            var httpClient = _httpClientFactory.CreateInternetExplorerMimickedHttpClient();
            using (Stream
                remoteStream = await httpClient.GetStreamAsync(eachItem.PackageUrl).ConfigureAwait(false),
                fileStream = File.OpenWrite(tempFilePath))
            {
                await remoteStream.CopyToAsync(fileStream, 81920, cancellationToken).ConfigureAwait(false);
            }

            eachItem.StatusMessage = UIStringResources.Hostess_Install_InProgress;

            if (parsedArgs.DryRun)
            {
                await Task.Delay(TimeSpan.FromSeconds(1d), cancellationToken).ConfigureAwait(false);
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

        private async Task ProcessPowerShellScriptAsync(InstallItemViewModel eachItem,
            CancellationToken cancellationToken = default)
        {
            var parsedArgs = _commandLineArguments.Current;

            eachItem.Installed = null;
            eachItem.StatusMessage = UIStringResources.Hostess_Install_InProgress;

            var downloadFolderPath = _sharedLocations.GetDownloadDirectoryPath();
            var tempFileName = $"bootstrap_{Guid.NewGuid():n}.ps1";
            var tempFilePath = System.IO.Path.Combine(downloadFolderPath, tempFileName);

            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);

            using (var stream = File.OpenWrite(tempFilePath))
            {
                using (var streamWriter = new StreamWriter(stream, Encoding.Unicode))
                {
                    await streamWriter.WriteAsync(eachItem.ScriptContent).ConfigureAwait(false);
                }
            }

            var powershellPath = _sharedLocations.GetDefaultPowerShellExecutableFilePath();

            if (!File.Exists(powershellPath))
                throw new Exception(ErrorStrings.Error_No_WindowsPowerShell);

            if (parsedArgs.DryRun)
            {
                await Task.Delay(TimeSpan.FromSeconds(1d), cancellationToken).ConfigureAwait(false);
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

        private async Task OpenAddInWebSiteAsync(InstallItemViewModel viewModel,
            CancellationToken cancellationToken = default)
        {
            var parsedArgs = _commandLineArguments.Current;

            if (parsedArgs.DryRun)
            {
                await Task.Delay(TimeSpan.FromSeconds(1d), cancellationToken).ConfigureAwait(false);
                return;
            }

            Process.Start(new ProcessStartInfo(viewModel.PackageUrl)
            {
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Maximized,
            });
        }

        private async Task OpenRequestedWebSiteAsync(string targetUrl,
            CancellationToken cancellationToken = default)
        {
            var parsedArgs = _commandLineArguments.Current;

            if (parsedArgs.DryRun)
            {
                await Task.Delay(TimeSpan.FromSeconds(1d), cancellationToken).ConfigureAwait(false);
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
