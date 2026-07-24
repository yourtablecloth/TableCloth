using AsyncAwaitBestPractices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using TableCloth.Components;
using TableCloth.Events;

namespace TableCloth;

/// <summary>
/// 이슈 #296: WPF <c>Application</c> → Avalonia <see cref="Application"/>. Lemon.Hosting 이 DI 로 이 App 을 생성하며
/// (<see cref="ActivatorUtilitiesConstructorAttribute"/>) 서비스를 주입한다. WPF <c>Application_Startup</c> 의
/// 스플래시→메인창 흐름은 <see cref="OnFrameworkInitializationCompleted"/> 로 이관한다.
/// </summary>
public partial class TableClothApplication : Application
{
    public TableClothApplication() { }

    [ActivatorUtilitiesConstructor]
    public TableClothApplication(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        ServiceProvider = serviceProvider;

        SafeFireAndForgetExtensions.Initialize();
        SafeFireAndForgetExtensions.SetDefaultExceptionHandling(thrownException =>
        {
            try
            {
                serviceProvider.GetRequiredService<ILogger<TableClothApplication>>()
                    .LogError(thrownException, "Unexpected error occurred.");

                if (Helpers.IsDevelopmentBuild)
                    serviceProvider.GetRequiredService<IAppMessageBox>().DisplayError(thrownException, false);
            }
            catch { /* 로거/메시지박스 자체가 비정상이면 무시 */ }
        });
    }

    private readonly IServiceProvider? _serviceProvider;
    private SplashScreen? _splashScreen;

    /// <summary>WPF Application.Properties 를 대체하는 정적 서비스 프로바이더 홀더(컨버터 등 뷰 계층 접근용).</summary>
    public static IServiceProvider? ServiceProvider { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && _serviceProvider != null)
        {
            // 스플래시 → (초기화 완료 시) 메인창. 초기화 중에는 메인창이 없으므로 명시 종료 모드로 둔다.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 이슈 #296: WPF 시절 Program.cs 의 호스트-전(前) 라이선스 게이트를 App 라이프사이클로 이관.
            // Avalonia 창은 라이프타임 시작 후에만 띄울 수 있으므로 스플래시보다 먼저 여기서 처리한다.
            if (!Bootstrap.LicenseGate.EnsureAgreed())
            {
                desktop.Shutdown(1);
                return;
            }

            var appUserInterface = _serviceProvider.GetRequiredService<IAppUserInterface>();
            _splashScreen = appUserInterface.CreateSplashScreen();
            _splashScreen.ViewModel.InitializeDone += (_, e) => OnInitializeDone(desktop, e);
            _splashScreen.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnInitializeDone(IClassicDesktopStyleApplicationLifetime desktop, DialogRequestEventArgs e)
    {
        _splashScreen?.Hide();

        if (e.DialogResult.HasValue && e.DialogResult.Value && _serviceProvider != null)
        {
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }
        else
        {
            desktop.Shutdown();
        }

        _splashScreen?.Close();
    }
}
