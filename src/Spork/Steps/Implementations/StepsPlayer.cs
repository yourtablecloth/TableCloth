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
            var stepsList = composedSteps.ToList();

            // 1단계: 모든 다운로드를 백그라운드에서 병렬로 시작
            var downloadTasks = StartBackgroundDownloads(stepsList, parsedArgs.DryRun, cancellationToken);

            // 2단계: 설치는 순차적으로 진행 (다운로드 완료를 기다린 후 설치)
            foreach (var eachItem in stepsList)
            {
                try
                {
                    eachItem.Installed = null;
                    eachItem.ShowProgress = true;
                    eachItem.ProgressRate = CalculateProgressRate(1, 0d);

                    // 해당 Step의 다운로드 완료 대기
                    eachItem.StatusMessage = UIStringResources.Spork_Download_InProgress;
                    eachItem.ProgressRate = CalculateProgressRate(2, 0d);

                    await WaitForContentLoadAsync(eachItem, downloadTasks, cancellationToken).ConfigureAwait(false);

                    // 다운로드 중 예외 발생 시 처리
                    if (eachItem.ContentLoadException != null)
                        throw eachItem.ContentLoadException;

                    eachItem.ProgressRate = CalculateProgressRate(2, 1d);

                    // 설치 진행
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

        /// <summary>
        /// 모든 Step의 다운로드를 백그라운드에서 병렬로 시작합니다.
        /// </summary>
        private Dictionary<StepItemViewModel, Task> StartBackgroundDownloads(
            List<StepItemViewModel> steps,
            bool isDryRun,
            CancellationToken cancellationToken)
        {
            var downloadTasks = new Dictionary<StepItemViewModel, Task>();

            foreach (var eachItem in steps)
            {
                var task = DownloadContentAsync(eachItem, isDryRun, cancellationToken);
                downloadTasks[eachItem] = task;
            }

            return downloadTasks;
        }

        /// <summary>
        /// 개별 Step의 콘텐츠를 다운로드합니다.
        /// </summary>
        private async Task DownloadContentAsync(
            StepItemViewModel item,
            bool isDryRun,
            CancellationToken cancellationToken)
        {
            try
            {
                if (isDryRun && item.Step.ShouldSimulateWhenDryRun)
                {
                    await Task.Delay(TimeSpan.FromSeconds(0.5d), cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await item.Step.LoadContentForStepAsync(
                        item.Argument,
                        (value) => item.ProgressRate = CalculateProgressRate(2, value),
                        cancellationToken).ConfigureAwait(false);
                }

                item.IsContentLoaded = true;
            }
            catch (Exception ex)
            {
                item.ContentLoadException = ex;
                item.IsContentLoaded = true; // 완료로 표시하여 대기 중인 코드가 진행되도록 함
            }
        }

        /// <summary>
        /// 특정 Step의 다운로드 완료를 대기합니다.
        /// </summary>
        private async Task WaitForContentLoadAsync(
            StepItemViewModel item,
            Dictionary<StepItemViewModel, Task> downloadTasks,
            CancellationToken cancellationToken)
        {
            if (downloadTasks.TryGetValue(item, out var downloadTask))
            {
                await downloadTask.ConfigureAwait(false);
            }
        }
    }
}
