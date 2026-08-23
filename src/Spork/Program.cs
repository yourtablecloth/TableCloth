using Avalonia;
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

                // Spork 모듈 합성: SporkAnswers/컬처 + Sentry/로깅/HTTP/Components/Browsers/Steps/UI.
                var builder = Host.CreateApplicationBuilder(args);
                builder.UseSpork();
                using var appHost = builder.Build();

                // 이슈 #296: WPF Application.Run → 표준 Avalonia 기동. App 은 정적 홀더로 서비스 프로바이더를 받는다.
                // (Lemon.Hosting 의 자체 run 루프는 Avalonia 11.3.x 와 비호환 — Dispatcher.MainLoop PlatformNotSupported.)
                SporkApplication.ServiceProvider = appHost.Services;
                SporkAvaloniaApp.Configure(AppBuilder.Configure<SporkApplication>())
                    .StartWithClassicDesktopLifetime(args);
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
