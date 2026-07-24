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
    /// 이슈 #296: WPF <c>Application</c> → Avalonia <see cref="Application"/>. Lemon.Hosting 이 DI 로 이 App 을
    /// 생성하며(<see cref="ActivatorUtilitiesConstructorAttribute"/>) 필요한 서비스를 주입한다. WPF 시절의
    /// <c>Application_Startup</c> 비동기 파이프라인은 <see cref="OnFrameworkInitializationCompleted"/> 로 이관한다.
    /// </summary>
    public partial class SporkApplication : Application
    {
        /// <summary>XAML 로더/디자이너 호환용. 런타임은 DI 생성자를 사용한다.</summary>
        public SporkApplication() { }

        [ActivatorUtilitiesConstructor]
        public SporkApplication(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            ServiceProvider = serviceProvider;

            // WPF Application.Properties[IServiceProvider] 대체: Avalonia 에는 Properties 딕셔너리가 없어
            // 정적 홀더로 서비스 프로바이더를 노출한다(RichTextBoxHelper 등 뷰 계층 정적 접근용이었으나,
            // 현 시점에선 코드비하인드에서 GetServiceProvider() 로 접근).

            _appMessageBox = serviceProvider.GetRequiredService<IAppMessageBox>();
            _commandLineArguments = serviceProvider.GetRequiredService<ICommandLineArguments>();
            _appStartup = serviceProvider.GetRequiredService<IAppStartup>();
            _appUserInterface = serviceProvider.GetRequiredService<IAppUserInterface>();
            _logger = serviceProvider.GetRequiredService<ILogger<SporkApplication>>();

            SafeFireAndForgetExtensions.Initialize();
            SafeFireAndForgetExtensions.SetDefaultExceptionHandling(thrownException =>
            {
                try { _logger?.LogError(thrownException, "Unexpected error occurred in fire-and-forget task."); }
                catch { /* 로거 자체가 비정상이면 무시 */ }
            });
        }

        private readonly IServiceProvider? _serviceProvider;
        private readonly IAppMessageBox? _appMessageBox;
        private readonly ICommandLineArguments? _commandLineArguments;
        private readonly IAppStartup? _appStartup;
        private readonly IAppUserInterface? _appUserInterface;
        private readonly ILogger<SporkApplication>? _logger;

        /// <summary>WPF Application.Properties 를 대체하는 정적 서비스 프로바이더 홀더.</summary>
        public static IServiceProvider? ServiceProvider { get; private set; }

        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // 스타트업 파이프라인(요건 검사→초기화→메인창)이 비동기이고, 치명적 실패 시 창 없이 종료할 수 있으므로
                // 우선 명시 종료 모드로 둔다. 파이프라인이 성공하면 메인창을 표시하고 OnMainWindowClose 로 전환한다.
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                RunStartupAsync(desktop).SafeFireAndForget();
            }

            base.OnFrameworkInitializationCompleted();
        }

        private async Task RunStartupAsync(IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (_appMessageBox == null || _commandLineArguments == null || _appStartup == null || _appUserInterface == null)
                throw new InvalidOperationException("SporkApplication was not constructed via the DI constructor; cached services are missing.");

            var parsedArgs = _commandLineArguments.GetCurrent();

            if (parsedArgs.ShowCommandLineHelp)
            {
                _appMessageBox.DisplayInfo(await _commandLineArguments.GetHelpStringAsync(), AppMessageBoxButton.OK);
                desktop.Shutdown();
                return;
            }

            if (parsedArgs.ShowVersionHelp)
            {
                _appMessageBox.DisplayInfo(await _commandLineArguments.GetVersionStringAsync(), AppMessageBoxButton.OK);
                desktop.Shutdown();
                return;
            }

            var warnings = new List<string>();
            var result = await _appStartup.HasRequirementsMetAsync(warnings);

            if (!result.Succeed)
            {
                _appMessageBox.DisplayError(result.FailedReason, result.IsCritical);

                if (result.IsCritical)
                {
                    if (Helpers.IsDevelopmentBuild)
                        throw result.FailedReason ?? TableClothAppException.Issue();
                    desktop.Shutdown(-1);
                    return;
                }
            }

            if (warnings.Any())
                _appMessageBox.DisplayError(string.Join(Environment.NewLine + Environment.NewLine, warnings), false);

            result = await _appStartup.InitializeAsync(warnings);

            if (!result.Succeed)
            {
                _appMessageBox.DisplayError(result.FailedReason, result.IsCritical);

                if (result.IsCritical)
                {
                    if (Helpers.IsDevelopmentBuild)
                        throw result.FailedReason ?? TableClothAppException.Issue();
                    desktop.Shutdown(-1);
                    return;
                }
            }

            var mainWindow = _appUserInterface.CreateMainWindow();
            desktop.MainWindow = mainWindow;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }
    }
}
