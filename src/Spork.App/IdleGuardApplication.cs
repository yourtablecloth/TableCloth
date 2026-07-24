using Microsoft.Extensions.Logging.Abstractions;
using Spork.Components.Implementations;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using TableCloth.Models.Answers;
using TableCloth.Serialization;

namespace Spork
{
    /// <summary>
    /// [미리 보기] 유휴 자동 종료 전용 헤드리스 WPF 애플리케이션(이슈 #197). <c>TableCloth.exe idle-guard</c>
    /// verb로 부팅 시 Spork 런처와 독립된 별도 프로세스로 기동된다. 메인 창이 없고
    /// (<see cref="ShutdownMode.OnExplicitShutdown"/>) 필요할 때만 경고 창을 띄우므로, 사용자가 Spork 창을
    /// 닫아도(또는 아예 열지 않아도) 유휴 보호가 계속 유지된다. 정책이 꺼져 있으면 즉시 종료한다.
    /// </summary>
    public sealed class IdleGuardApplication : Application
    {
        public IdleGuardApplication()
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Startup += OnStartup;
        }

        private void OnStartup(object sender, StartupEventArgs e)
        {
            try
            {
                IdleGuardDiagnostics.Log("idle-guard process started.");

                // 경고 창 문구를 호스트 로캘로 맞춘다(가드는 UseSpork의 컬처 적용 흐름을 타지 않으므로 직접 처리).
                ApplyHostCultureIfPresent();

                var monitor = new SessionIdleMonitor(
                    new SporkAnswersSessionPolicyProvider(),
                    NullLogger<SessionIdleMonitor>.Instance);

                // 정책이 비활성이면(기능 꺼짐) 가드는 할 일이 없으므로 즉시 종료한다.
                if (!monitor.Start())
                {
                    IdleGuardDiagnostics.Log("policy disabled or app not ready; guard exiting.");
                    Shutdown();
                }
            }
            catch (Exception ex)
            {
                // 가드는 best-effort. 초기화 실패가 대화상자로 튀어 세션을 방해하지 않도록 조용히 종료한다.
                IdleGuardDiagnostics.Log($"startup failed: {ex}");
                Debug.WriteLine($"[idle-guard] startup failed: {ex}");
                Shutdown();
            }
        }

        private static void ApplyHostCultureIfPresent()
        {
            try
            {
                var answerFilePath = Path.Combine(AppContext.BaseDirectory, "SporkAnswers.json");
                if (!File.Exists(answerFilePath))
                    return;

                using var stream = File.OpenRead(answerFilePath);
                var answers = JsonSerializer.Deserialize(stream, SporkJsonContext.Default.SporkAnswers);
                if (string.IsNullOrWhiteSpace(answers?.HostUILocale))
                    return;

                var culture = new CultureInfo(answers.HostUILocale);
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[idle-guard] apply culture failed: {ex}");
            }
        }
    }
}
