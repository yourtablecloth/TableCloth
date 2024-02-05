using Hostess.Components;
using Hostess.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Hostess.Steps.Implementations
{
    public sealed class ReloadEdgeStep : StepBase<InstallItemViewModel>
    {
        public ReloadEdgeStep(
            ISharedLocations sharedLocations)
        {
            _sharedLocations = sharedLocations;
        }

        private readonly ISharedLocations _sharedLocations;

        public override Task LoadContentForStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override async Task PlayStepAsync(InstallItemViewModel _, CancellationToken cancellationToken = default)
        {
            // msedge.exe 파일 경로를 유추하고, Policy를 반영하기 위해 잠시 실행했다가 종료하는 동작을 추가
            if (!_sharedLocations.TryGetMicrosoftEdgeExecutableFilePath(out var msedgePath))
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
                    await Task.Delay(TimeSpan.FromSeconds(3d), cancellationToken).ConfigureAwait(false);
                    msedgeProcess.CloseMainWindow();
                    await tcs.Task.ConfigureAwait(false);
                }
            }
        }

        public override bool ShouldSimulateWhenDryRun
            => true;
    }
}
