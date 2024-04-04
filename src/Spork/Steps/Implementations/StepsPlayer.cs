using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Spork.Browsers;
using Spork.Components;
using Spork.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Resources;

namespace Spork.Steps.Implementations
{
    public sealed class StepsPlayer : IStepsPlayer
    {
        public StepsPlayer(
            IResourceCacheManager resourceCacheManager,
            ICommandLineArguments commandLineArguments,
            IWebBrowserServiceFactory webBrowserServiceFactory)
        {
            _resourceCacheManager = resourceCacheManager;
            _commandLineArguments = commandLineArguments;
            _webBrowserServiceFactory = webBrowserServiceFactory;
            _defaultWebBrowserService = _webBrowserServiceFactory.GetWindowsSandboxDefaultBrowserService();
        }

        private readonly IResourceCacheManager _resourceCacheManager;
        private readonly ICommandLineArguments _commandLineArguments;
        private readonly IWebBrowserServiceFactory _webBrowserServiceFactory;
        private readonly IWebBrowserService _defaultWebBrowserService;

        public bool IsRunning { get; private set; }

        private const double PreparationProgress = 33.0;
        private const double LoadingProgress = 66.0;
        private const double PerformingProgress = 100.0;

        // 현재 단계의 진행률을 업데이트합니다. value는 0에서 1 사이여야 합니다.
        private double CalculateProgressRate(int stage, double value)
        {
            switch (stage)
            {
                case 1:
                    // 첫 번째 단계의 진행률 계산
                    return PreparationProgress * value;
                case 2:
                    // 두 번째 단계의 진행률 계산
                    // 여기서 value는 중간 단계의 진행률을 나타내며, 0에서 1 사이의 값입니다.
                    return PreparationProgress + (LoadingProgress - PreparationProgress) * value;
                case 3:
                    // 세 번째 단계의 진행률 계산
                    return LoadingProgress + (PerformingProgress - LoadingProgress) * value;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stage), "Stage must be between 1 and 3.");
            }
        }

        public async Task<bool> PlayStepsAsync(
            IEnumerable<StepItemViewModel> composedSteps,
            bool dryRun,
            CancellationToken cancellationToken = default)
        {
            var parsedArgs = _commandLineArguments.GetCurrent();
            var hasAnyFailure = false;

            IsRunning = true;
            var catalog = _resourceCacheManager.CatalogDocument;

            foreach (var eachItem in composedSteps)
            {
                try
                {
                    eachItem.Installed = null;
                    eachItem.ShowProgress = true;
                    eachItem.ProgressRate = CalculateProgressRate(1, 0d);

                    eachItem.StatusMessage = UIStringResources.Spork_Download_InProgress;
                    eachItem.ProgressRate = CalculateProgressRate(2, 0d);
                    if (parsedArgs.DryRun && eachItem.Step.ShouldSimulateWhenDryRun)
                    {
                        eachItem.ProgressRate = CalculateProgressRate(2, 0.5d);
                        await Task.Delay(TimeSpan.FromSeconds(0.5d), cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await eachItem.Step.LoadContentForStepAsync(
                            eachItem.Argument,
                            (value) => eachItem.ProgressRate = CalculateProgressRate(2, value),
                            cancellationToken).ConfigureAwait(false);
                    }
                    eachItem.ProgressRate = CalculateProgressRate(2, 1d);

                    eachItem.StatusMessage = UIStringResources.Spork_Install_InProgress;
                    eachItem.ProgressRate = CalculateProgressRate(3, 0d);
                    if (parsedArgs.DryRun && eachItem.Step.ShouldSimulateWhenDryRun)
                    {
                        eachItem.ProgressRate = CalculateProgressRate(3, 0.5d);
                        await Task.Delay(TimeSpan.FromSeconds(0.5d), cancellationToken).ConfigureAwait(false);
                    }
                    else if (await eachItem.Step.EvaluateRequiredStepAsync(eachItem.Argument, cancellationToken).ConfigureAwait(false))
                    {
                        await eachItem.Step.PlayStepAsync(
                            eachItem.Argument,
                            (value) => eachItem.ProgressRate = CalculateProgressRate(3, value),
                            cancellationToken).ConfigureAwait(false);
                    }
                    eachItem.ProgressRate = CalculateProgressRate(3, 1d);

                    eachItem.StatusMessage = UIStringResources.Spork_Install_Succeed;
                    eachItem.Installed = true;
                    eachItem.ErrorMessage = null;
                }
                catch (Exception ex)
                {
                    hasAnyFailure = true;
                    eachItem.StatusMessage = UIStringResources.Spork_Install_Failed;
                    eachItem.Installed = false;
                    eachItem.ErrorMessage = ex is AggregateException exception ? exception.InnerException.ToString() : ex.ToString();
                    await Task.Delay(TimeSpan.FromMilliseconds(100d), cancellationToken).ConfigureAwait(false);
                }
                finally { eachItem.ShowProgress = false; }
            }

            IsRunning = false;

            if (!hasAnyFailure)
            {
                var targets = parsedArgs.SelectedServices;

                foreach (var eachUrl in catalog.Services.Where(x => targets.Contains(x.Id)).Select(x => x.Url))
                    Process.Start(_defaultWebBrowserService.CreateWebPageOpenRequest(eachUrl, ProcessWindowStyle.Maximized));
            }

            return hasAnyFailure;
        }
    }
}
