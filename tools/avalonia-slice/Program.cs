using System;
using Avalonia;
using Lemon.Hosting.AvaloniauiDesktop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AvaloniaSlice;

internal static class Program
{
    // M2c: Microsoft.Extensions.Hosting + Lemon.Hosting + Avalonia 통합이 Native AOT에서 동작하는지 실증.
    // (실 앱은 verb 디스패치 + UseTableCloth()/UseSpork() DI 합성 후 이 방식으로 Avalonia를 띄운다.)
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // 서비스 + VM 을 DI 에 등록. 실 앱의 서비스/VM 등록과 동일한 형태.
        builder.Services.AddSingleton<IGreetingService, GreetingService>();
        builder.Services.AddSingleton<MainViewModel>();

        // NOTE(M3): Lemon.Hosting 1.1.1 은 이 두 API 를 obsolete 처리(→ AddAppBuilder / RunAvaloniaAppAsync).
        // 본 슬라이스는 AOT 통합 검증이 목적이라 vNext 가 검증한 기존 API 를 유지한다. 실 앱(M3)에서는 신 API 로 이관.
#pragma warning disable CS0618 // Type or member is obsolete
        builder.Services.AddAvaloniauiDesktopApplication<App>(BuildAvaloniaApp);

        using var app = builder.Build();
        app.RunAvaloniauiApplication(args);
#pragma warning restore CS0618
    }

    private static AppBuilder BuildAvaloniaApp(AppBuilder appBuilder)
        => appBuilder
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
