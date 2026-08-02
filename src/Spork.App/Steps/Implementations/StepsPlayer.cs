using Spork.Components;
using Spork.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Resources;

namespace Spork.Steps.Implementations
{
    public sealed class StepsPlayer : IStepsPlayer
    {
        // 사이트 열기를 호출자로 넘기면서 카탈로그·브라우저·메시지 박스 의존이 모두 필요 없어졌다.
        // 남는 것은 실행 옵션(DryRun)뿐이다.
        public StepsPlayer(
            ICommandLineArguments commandLineArguments)
        {
            _commandLineArguments = commandLineArguments;
        }

        private readonly ICommandLineArguments _commandLineArguments;

        public bool IsRunning { get; private set; }

        private const double PreparationProgress = 33.0;
        private const double LoadingProgress = 66.0;
        private const double PerformingProgress = 100.0;
        private static readonly TimeSpan[] DownloadRetryDelays = [TimeSpan.FromSeconds(1d), TimeSpan.FromSeconds(3d)];

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

            // 사이트 열기는 여기서 하지 않는다. 호출자(MainWindowViewModel / 카탈로그 설치 모달)가
            // 열어야 할 주소를 알고 있고, 딥링크로 지정된 대상 URL 은 카탈로그 도메인 게이트를 통과한
            // 값이어야 하기 때문이다. 예전에는 여기서도 카탈로그 대표 URL 을 열어 호출자의 열기와
            // 겹쳤고, 딥링크에서는 대표 URL 과 대상 URL 이 탭 두 개로 떴다(실측 제보).
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
                    await LoadStepContentWithRetryAsync(item, cancellationToken).ConfigureAwait(false);
                }

                item.IsContentLoaded = true;
            }
            catch (Exception ex)
            {
                item.ContentLoadException = ex;
                item.IsContentLoaded = true; // 완료로 표시하여 대기 중인 코드가 진행되도록 함
            }
        }

        private async Task LoadStepContentWithRetryAsync(
            StepItemViewModel item,
            CancellationToken cancellationToken)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    await item.Step.LoadContentForStepAsync(
                        item.Argument,
                        (value) => item.ProgressRate = CalculateProgressRate(2, value),
                        cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                        throw;

                    var hasMoreRetry = attempt < DownloadRetryDelays.Length;
                    if (!hasMoreRetry || !IsTransientDownloadException(ex))
                        throw;

                    try
                    {
                        await Task.Delay(DownloadRetryDelays[attempt], cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                }
            }
        }

        private static bool IsTransientDownloadException(Exception exception)
        {
            if (exception is HttpRequestException httpRequestException)
            {
                // 응답 자체를 받지 못한 경우(연결 실패 등)는 StatusCode가 없다 → 일시적으로 취급.
                if (!httpRequestException.StatusCode.HasValue)
                    return true;

                // 상태 코드가 있으면 "재시도할 가치가 있는" 코드만 일시적. 그 외(4xx 대부분, 501/505 등
                // 영구성 5xx)는 재시도하지 않고 즉시 실패로 건너뛴다.
                return IsRetryableStatusCode(httpRequestException.StatusCode.Value);
            }

            if (exception is TaskCanceledException || exception is TimeoutException)
                return true;

            if (exception is IOException)
                return true;

            // 소켓 수준 오류(연결 거부·리셋·타임아웃, DNS 해석 실패 등). HttpClient는 대개 이를
            // HttpRequestException(StatusCode 없음)이나 IOException으로 래핑하지만, 래핑되지 않은 순수
            // SocketException이 올라오는 경로(다른 다운로드 계층/핸들러)도 일시적으로 취급한다.
            if (exception is SocketException)
                return true;

            return exception.InnerException != null && IsTransientDownloadException(exception.InnerException);
        }

        /// <summary>
        /// 재시도할 가치가 있는(일시적) HTTP 상태 코드인지 여부. 일시적인 서버측 오류/과부하만 true로 하고,
        /// 요청 자체의 문제(4xx 대부분)나 영구성 서버 오류(501 Not Implemented, 505 등)는 false로 하여
        /// 재시도하지 않고 건너뛴다.
        /// </summary>
        private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
            => statusCode == HttpStatusCode.RequestTimeout          // 408
            || statusCode == HttpStatusCode.TooManyRequests         // 429
            || statusCode == HttpStatusCode.InternalServerError     // 500
            || statusCode == HttpStatusCode.BadGateway              // 502
            || statusCode == HttpStatusCode.ServiceUnavailable      // 503
            || statusCode == HttpStatusCode.GatewayTimeout;         // 504

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
