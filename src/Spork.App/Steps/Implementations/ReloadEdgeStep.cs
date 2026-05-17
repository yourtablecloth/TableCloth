using Spork.ViewModels;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Spork.Steps.Implementations
{
    /// <summary>
    /// 직전 단계들이 기록한 Microsoft Edge 정책(LNA/PNA 허용 등)이 다음 사이트 오픈 시 새 인스턴스에
    /// 즉시 반영되도록, 현재 실행 중인 msedge 프로세스 트리를 모두 강제 종료한다.
    /// </summary>
    /// <remarks>
    /// 이전 구현은 <c>msedge.exe about:blank</c>를 잠깐 띄웠다 닫는 패턴이었는데 두 가지 문제가 있었다:
    /// <list type="number">
    ///   <item>
    ///     Chromium은 단일 인스턴스로 동작하므로 이미 실행 중인 Edge가 있으면 <c>Process.Start</c>가
    ///     돌려준 핸들은 위임 즉시 종료되는 launcher 프로세스에 불과하다. 진짜 Edge 본체는 우리가
    ///     잡지 못한 채 계속 살아남아 정책이 반영된 fresh 인스턴스가 만들어지지 않았다.
    ///   </item>
    ///   <item>
    ///     <c>Process.Start</c> 직후 launcher가 빠르게 exit하면 그 뒤에 attach한 <c>Exited</c>
    ///     이벤트 핸들러는 발화하지 않는다. <c>TaskCompletionSource</c>가 절대 완료되지 않아
    ///     <c>await tcs.Task</c>가 무한 대기에 빠지는 lock 버그가 자주 재현되었다.
    ///   </item>
    /// </list>
    /// 본 구현은 launch-then-close 흐름을 폐기하고 단순히 <c>msedge</c> 이름의 모든 프로세스를
    /// 트리 단위로 Kill한다. 사용자가 이후 카탈로그에서 사이트를 열 때 fresh Edge가 시작되며 그
    /// 시점에 새 정책을 읽어 들인다.
    /// </remarks>
    public sealed class ReloadEdgeStep : StepBase<InstallItemViewModel>
    {
        public ReloadEdgeStep()
        {
        }

        private const string EdgeProcessName = "msedge";

        public override Task<bool> EvaluateRequiredStepAsync(InstallItemViewModel viewModel, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public override Task LoadContentForStepAsync(InstallItemViewModel viewModel, Action<double> progressCallback, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public override async Task PlayStepAsync(InstallItemViewModel _, Action<double> progressCallback, CancellationToken cancellationToken = default)
        {
            progressCallback?.Invoke(10d);

            // msedge.exe + GPU/Renderer/Utility 자식 프로세스 모두 "msedge" 이름. 부모를 entireProcessTree=true로
            // 죽이면 자식도 따라 종료되지만, Chromium은 watcher가 자식 일부를 재기동하는 케이스가 있으므로
            // 안전을 위해 enumerate 후 모두 개별 Kill 한다.
            var msedgeProcesses = Process.GetProcessesByName(EdgeProcessName);

            if (msedgeProcesses.Length == 0)
            {
                progressCallback?.Invoke(100d);
                return;
            }

            foreach (var process in msedgeProcesses)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // 권한 부족 / 이미 종료 등은 무시. 다음 launch에서 fresh 인스턴스가 시작되도록
                    // best-effort로 진행한다.
                }
                finally
                {
                    process.Dispose();
                }
            }

            progressCallback?.Invoke(60d);

            // 모든 인스턴스가 실제로 사라질 때까지 짧게 대기. 5초 cap으로 hang 방지.
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                var remaining = Process.GetProcessesByName(EdgeProcessName);
                foreach (var p in remaining)
                    p.Dispose();

                if (remaining.Length == 0)
                    break;

                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }

            progressCallback?.Invoke(100d);
        }

        public override bool ShouldSimulateWhenDryRun
            => true;
    }
}
