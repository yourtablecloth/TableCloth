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
/// 이슈 #296: WPF <c>Application</c> → Avalonia <see cref="Application"/>. 표준 Avalonia 기동
/// (<c>AppBuilder.Configure&lt;TableClothApplication&gt;().StartWithClassicDesktopLifetime</c>)을 사용하므로 이 App 은
/// 매개변수 없는 생성자로 만들어진다. 진입점(Program)이 Host 를 빌드해 <see cref="ServiceProvider"/> 정적 홀더에
/// 주입한 뒤 기동한다. WPF <c>Application_Startup</c> 의 스플래시→메인창 흐름과 라이선스 게이트를
/// <see cref="OnFrameworkInitializationCompleted"/> 로 이관.
/// (Lemon.Hosting 의 자체 run 루프는 Avalonia 11.3.x 의 Dispatcher.MainLoop 와 비호환 — PlatformNotSupported 폐기.)
/// </summary>
public partial class TableClothApplication : Application
{
    public TableClothApplication() { }

    /// <summary>진입점이 StartWithClassicDesktopLifetime 호출 전에 주입한다(컨버터 등 뷰 계층 정적 접근용).</summary>
    public static IServiceProvider? ServiceProvider { get; set; }

    private SplashScreen? _splashScreen;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && ServiceProvider is { } sp)
        {
            SafeFireAndForgetExtensions.Initialize();
            SafeFireAndForgetExtensions.SetDefaultExceptionHandling(thrownException =>
            {
                try
                {
                    sp.GetService<ILogger<TableClothApplication>>()?.LogError(thrownException, "Unexpected error occurred.");
                    if (Helpers.IsDevelopmentBuild)
                        sp.GetService<IAppMessageBox>()?.DisplayError(thrownException, false);
                }
                catch { /* 로거/메시지박스 자체가 비정상이면 무시 */ }
            });

            // 초기화 중에는 메인창이 없으므로 명시 종료 모드로 둔다.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 이슈 #296: WPF 시절 Program.cs 의 호스트-전 라이선스 게이트를 App 라이프사이클로 이관.
            if (!Bootstrap.LicenseGate.EnsureAgreed())
            {
                desktop.Shutdown(1);
                base.OnFrameworkInitializationCompleted();
                return;
            }

            var appUserInterface = sp.GetRequiredService<IAppUserInterface>();
            _splashScreen = appUserInterface.CreateSplashScreen();
            _splashScreen.ViewModel.InitializeDone += (_, e) => OnInitializeDone(desktop, sp, e);
            _splashScreen.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void OnInitializeDone(IClassicDesktopStyleApplicationLifetime desktop, IServiceProvider sp, DialogRequestEventArgs e)
    {
        // 이슈 #296: 부팅이 인트로 애니메이션보다 빨리 끝나더라도, 애니메이션이 완전히 끝난 뒤에
        // 스플래시를 숨기고 닫는다(애니메이션 도중 창이 사라지지 않게). InitializeDone 은 UI 스레드
        // TaskFactory 로 발생하므로 await 이후 연속 실행도 UI 스레드다.
        if (_splashScreen is not null)
            await _splashScreen.IntroAnimationTask;

        _splashScreen?.Hide();

        if (e.DialogResult.HasValue && e.DialogResult.Value)
        {
            var mainWindow = sp.GetRequiredService<MainWindow>();
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
