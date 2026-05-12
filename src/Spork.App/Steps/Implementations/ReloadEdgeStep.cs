using Microsoft.Extensions.DependencyInjection;
using Spork.Browsers;
using Spork.Browsers.Implementations;
using Spork.ViewModels;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Spork.Steps.Implementations
{
    public sealed class ReloadEdgeStep : StepBase<InstallItemViewModel>
    {
        public ReloadEdgeStep(
            [FromKeyedServices(nameof(X86ChromiumEdgeWebBrowserService))] IWebBrowserService expectedWebBrowserService)
        {
            _expectedWebBrowserService = expectedWebBrowserService;
        }

        private readonly IWebBrowserService _expectedWebBrowserService;

        public override Task<bool> EvaluateRequiredStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public override Task LoadContentForStepAsync(InstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override async Task PlayStepAsync(InstallItemViewModel _, Action<double> progressCallback, CancellationToken cancellationToken = default)
        {
            if (!_expectedWebBrowserService.TryGetBrowserExecutablePath(out var browserExecutablePath))
                return;

            // msedge.exe 파일 경로를 유추하고, Policy를 반영하기 위해 잠시 실행했다가 종료하는 동작을 추가
            var msedgePsi = new ProcessStartInfo(browserExecutablePath, "about:blank")
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
                await Task.Delay(TimeSpan.FromSeconds(3d), cancellationToken).ConfigureAwait(false);
                msedgeProcess.CloseMainWindow();
                await tcs.Task.ConfigureAwait(false);
            }
        }

        public override bool ShouldSimulateWhenDryRun
            => true;
    }
}
