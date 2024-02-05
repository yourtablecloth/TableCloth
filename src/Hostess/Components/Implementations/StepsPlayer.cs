using Hostess.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
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
            IEnumerable<StepItemViewModel> composedSteps,
            bool dryRun,
            CancellationToken cancellationToken = default)
        {
            var parsedArgs = _commandLineArguments.Current;
            var hasAnyFailure = false;

            IsRunning = true;
            var catalog = _resourceCacheManager.CatalogDocument;

            foreach (var eachItem in composedSteps)
            {
                try
                {
                    eachItem.Installed = null;

                    eachItem.StatusMessage = UIStringResources.Hostess_Download_InProgress;
                    if (parsedArgs.DryRun && eachItem.Step.ShouldSimulateWhenDryRun)
                        await Task.Delay(TimeSpan.FromSeconds(0.5d), cancellationToken).ConfigureAwait(false);
                    else
                        await eachItem.Step.LoadContentForStepAsync(eachItem.Argument, cancellationToken).ConfigureAwait(false);

                    eachItem.StatusMessage = UIStringResources.Hostess_Install_InProgress;
                    if (parsedArgs.DryRun && eachItem.Step.ShouldSimulateWhenDryRun)
                        await Task.Delay(TimeSpan.FromSeconds(0.5d), cancellationToken).ConfigureAwait(false);
                    else
                        await eachItem.Step.PlayStepAsync(eachItem.Argument, cancellationToken).ConfigureAwait(false);

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
                var targets = parsedArgs.SelectedServices;

                foreach (var eachUrl in catalog.Services.Where(x => targets.Contains(x.Id)).Select(x => x.Url))
                {
                    Process.Start(new ProcessStartInfo(eachUrl)
                    {
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Maximized,
                    });
                }
            }

            return hasAnyFailure;
        }
    }
}
