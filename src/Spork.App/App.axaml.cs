using AsyncAwaitBestPractices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spork.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TableCloth;
using TableCloth.Models;

namespace Spork
{
    /// <summary>
    /// 이슈 #296: WPF <c>Application</c> → Avalonia <see cref="Application"/>. 표준 Avalonia 기동
    /// (<c>AppBuilder.Configure&lt;SporkApplication&gt;().StartWithClassicDesktopLifetime</c>)을 사용하므로 이 App 은
    /// 매개변수 없는 생성자로 만들어진다. 진입점(Program)이 Host 를 빌드해 <see cref="ServiceProvider"/> 정적 홀더에
    /// 주입한 뒤 기동하며, WPF <c>Application_Startup</c> 파이프라인은 <see cref="OnFrameworkInitializationCompleted"/> 로 이관.
    /// (Lemon.Hosting 의 자체 run 루프는 Avalonia 11.3.x 의 Dispatcher.MainLoop 와 호환되지 않아 폐기 — PlatformNotSupported.)
    /// </summary>
    public partial class SporkApplication : Application
    {
        public SporkApplication() { }

        /// <summary>진입점이 StartWithClassicDesktopLifetime 호출 전에 주입한다.</summary>
        public static IServiceProvider? ServiceProvider { get; set; }

        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && ServiceProvider is { } sp)
            {
                SafeFireAndForgetExtensions.Initialize();
                SafeFireAndForgetExtensions.SetDefaultExceptionHandling(thrownException =>
                {
                    try { sp.GetService<ILogger<SporkApplication>>()?.LogError(thrownException, "Unexpected error occurred in fire-and-forget task."); }
                    catch { /* 로거 자체가 비정상이면 무시 */ }
                });

                // 스타트업 파이프라인(요건 검사→초기화→메인창)이 비동기이고 치명 실패 시 창 없이 종료할 수 있으므로
                // 우선 명시 종료 모드로 둔다. 성공 시 메인창 표시 + OnMainWindowClose 로 전환.
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                RunStartupAsync(desktop, sp).SafeFireAndForget();
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static async Task RunStartupAsync(IClassicDesktopStyleApplicationLifetime desktop, IServiceProvider sp)
        {
            var appMessageBox = sp.GetRequiredService<IAppMessageBox>();
            var commandLineArguments = sp.GetRequiredService<ICommandLineArguments>();
            var appStartup = sp.GetRequiredService<IAppStartup>();
            var appUserInterface = sp.GetRequiredService<IAppUserInterface>();

            var parsedArgs = commandLineArguments.GetCurrent();

            if (parsedArgs.ShowCommandLineHelp)
            {
                appMessageBox.DisplayInfo(await commandLineArguments.GetHelpStringAsync(), AppMessageBoxButton.OK);
                desktop.Shutdown();
                return;
            }

            if (parsedArgs.ShowVersionHelp)
            {
                appMessageBox.DisplayInfo(await commandLineArguments.GetVersionStringAsync(), AppMessageBoxButton.OK);
                desktop.Shutdown();
                return;
            }

            var warnings = new List<string>();
            var result = await appStartup.HasRequirementsMetAsync(warnings);

            if (!result.Succeed)
            {
                appMessageBox.DisplayError(result.FailedReason, result.IsCritical);

                if (result.IsCritical)
                {
                    if (Helpers.IsDevelopmentBuild)
                        throw result.FailedReason ?? TableClothAppException.Issue();
                    desktop.Shutdown(-1);
                    return;
                }
            }

            if (warnings.Any())
                appMessageBox.DisplayError(string.Join(Environment.NewLine + Environment.NewLine, warnings), false);

            result = await appStartup.InitializeAsync(warnings);

            if (!result.Succeed)
            {
                appMessageBox.DisplayError(result.FailedReason, result.IsCritical);

                if (result.IsCritical)
                {
                    if (Helpers.IsDevelopmentBuild)
                        throw result.FailedReason ?? TableClothAppException.Issue();
                    desktop.Shutdown(-1);
                    return;
                }
            }

            var mainWindow = appUserInterface.CreateMainWindow();
            desktop.MainWindow = mainWindow;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }
    }
}
