using Microsoft.Extensions.Logging;
using Spork.Dialogs;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace Spork.Components.Implementations
{
    /// <summary>
    /// [미리 보기] <see cref="ISessionIdleMonitor"/>의 기본 구현(이슈 #197). <c>GetLastInputInfo</c>로 세션의
    /// 유휴 시간을 1초 간격으로 확인한다. 허용 시간 종료 <see cref="_warningLeadSeconds"/>초 전부터 경고 창을
    /// 띄워 카운트다운을 보여주고, 사용자가 다시 활동하면 경고를 닫는다. 그대로 시간이 다 되면 게스트에서
    /// <c>shutdown</c>으로 샌드박스를 종료한다. Data 디렉터리는 읽기-쓰기로 마운트되어 있어 급종료로도
    /// 즐겨찾기/기록이 손실되지 않는다(샌드박스는 일회용).
    /// </summary>
    public sealed class SessionIdleMonitor : ISessionIdleMonitor
    {
        public SessionIdleMonitor(
            ISessionPolicyProvider policyProvider,
            ILogger<SessionIdleMonitor> logger)
        {
            _policyProvider = policyProvider;
            _logger = logger;
        }

        private readonly ISessionPolicyProvider _policyProvider;
        private readonly ILogger _logger;

        private DispatcherTimer _timer;
        private IdleLogoutWarningWindow _warning;
        private int _timeoutSeconds;
        private int _warningLeadSeconds;
        private int _tickCount;
        private bool _shuttingDown;

        public bool Start()
        {
            var policy = _policyProvider.GetIdleAutoLogoutPolicy();
            if (!policy.Enabled || policy.TimeoutMinutes <= 0)
                return false; // 미리 보기 기능이 꺼져 있으면(기본값) 아무 것도 하지 않는다.

            _timeoutSeconds = policy.TimeoutMinutes * 60;
            // 종료 60초 전(단, 허용 시간의 절반을 넘지 않게)부터 경고를 띄운다.
            _warningLeadSeconds = Math.Min(60, Math.Max(1, _timeoutSeconds / 2));

            var app = Application.Current;
            if (app == null)
            {
                _logger?.LogWarning("Idle monitor: Application.Current is null; monitor not started.");
                return false;
            }

            // 감시 타이머는 UI 스레드에서 돌린다(경고 창 생성/갱신이 UI 작업이므로).
            app.Dispatcher.InvokeAsync(() =>
            {
                _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1d) };
                _timer.Tick += OnTick;
                _timer.Start();
                _logger?.LogInformation("Idle auto-logout monitor started (timeout={TimeoutSeconds}s).", _timeoutSeconds);
                IdleGuardDiagnostics.Log($"monitor armed: timeout={_timeoutSeconds}s, warningLead={_warningLeadSeconds}s.");
            });

            return true;
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (_shuttingDown)
                return;

            var idleSeconds = (int)GetIdleTime().TotalSeconds;
            var remaining = _timeoutSeconds - idleSeconds;

            // 15초마다 유휴 값을 남겨, 헤드리스 가드가 살아 있고 유휴가 실제로 누적되는지 진단할 수 있게 한다.
            if (++_tickCount % 15 == 0)
                IdleGuardDiagnostics.Log($"heartbeat: idle={idleSeconds}s, remaining={remaining}s.");

            if (remaining <= 0)
            {
                TriggerShutdown();
                return;
            }

            if (remaining <= _warningLeadSeconds)
            {
                if (_warning == null)
                {
                    IdleGuardDiagnostics.Log($"warning shown (remaining={remaining}s).");
                    _warning = new IdleLogoutWarningWindow();
                    _warning.Closed += (_, __) => _warning = null;
                    _warning.Show();
                }
                _warning.SetRemaining(remaining);
            }
            else if (_warning != null)
            {
                // 사용자가 다시 활동해 유휴 시간이 리셋됨 → 경고를 닫는다(Closed 핸들러가 참조를 비움).
                _warning.Close();
            }
        }

        private void TriggerShutdown()
        {
            _shuttingDown = true;
            _timer?.Stop();
            IdleGuardDiagnostics.Log("idle timeout reached; requesting sandbox shutdown.");

            try { _warning?.Close(); } catch { /* 무시 */ }

            try
            {
                // 게스트 OS를 종료해 인증된 세션(Edge/뱅킹)을 즉시 무력화한다(보안 목적 달성). Data는 RW 마운트라 보존.
                // 참고: 게스트 shutdown 은 WSB '창' 자체는 닫지 못한다(호스트가 창을 통제). 창까지 깔끔히 닫는 것은
                // 호스트 측 종료가 필요하며 별도 과제로 둔다(이슈 #197 코멘트 참조).
                Process.Start(new ProcessStartInfo("shutdown", "/s /f /t 0")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                });
                IdleGuardDiagnostics.Log("guest shutdown requested (session secured).");
                _logger?.LogInformation("Idle auto-logout: guest shutdown requested.");
            }
            catch (Exception ex)
            {
                IdleGuardDiagnostics.Log($"shutdown failed: {ex.Message}");
                _logger?.LogWarning(ex, "Idle auto-logout shutdown failed.");
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        /// <summary>세션의 마지막 입력 이후 경과 시간을 반환한다. 실패 시 0(유휴 아님으로 간주).</summary>
        private static TimeSpan GetIdleTime()
        {
            var lastInputInfo = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            if (!GetLastInputInfo(ref lastInputInfo))
                return TimeSpan.Zero;

            // TickCount는 uint로 순환하므로 unchecked 뺄셈으로 래핑을 안전하게 처리한다.
            var idleMilliseconds = unchecked((uint)Environment.TickCount - lastInputInfo.dwTime);
            return TimeSpan.FromMilliseconds(idleMilliseconds);
        }
    }
}
