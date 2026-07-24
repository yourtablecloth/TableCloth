using Lemon.Hosting.AvaloniauiDesktop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spork.App.DependencyInjection;
using System;
using System.Diagnostics;
using TableCloth;

namespace Spork
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                args ??= Helpers.GetCommandLineArguments();

                var builder = Host.CreateApplicationBuilder(args);

                // Spork 모듈 합성: SporkAnswers/컬처 + Sentry/로깅/HTTP/Components/Browsers/Steps/UI.
                builder.UseSpork();

                // 이슈 #296: WPF Application.Run → Avalonia(Lemon.Hosting). App(SporkApplication)은 DI 로 생성된다.
                // Lemon.Hosting 1.1.1 은 이 두 API 를 obsolete 처리(→ AddAppBuilder/RunAvaloniaAppAsync). M2c 가 AOT 로
                // 검증한 기존 API 를 1차로 유지하고, 신 API 이관은 green 확보 후 후속으로 진행한다.
#pragma warning disable CS0618 // Type or member is obsolete
                builder.Services.AddAvaloniauiDesktopApplication<SporkApplication>(SporkAvaloniaApp.Configure);
                using var appHost = builder.Build();
                appHost.RunAvaloniauiApplication(args);
#pragma warning restore CS0618
            }
            catch (Exception ex)
            {
                // Avalonia 기동 전/중 치명 오류. UI 가 아직 없을 수 있어 Debug 로만 남긴다(Sentry 는 UseSpork 에서 초기화됨).
                Debug.WriteLine($"[Spork] fatal: {ex}");
            }

            return Environment.ExitCode;
        }
    }
}
