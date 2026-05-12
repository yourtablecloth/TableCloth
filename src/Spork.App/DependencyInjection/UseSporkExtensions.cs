using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentry;
using Serilog;
using Spork.Browsers;
using Spork.Browsers.Implementations;
using Spork.Components;
using Spork.Components.Implementations;
using Spork.Dialogs;
using Spork.Steps;
using Spork.Steps.Implementations;
using Spork.ViewModels;
using System.Threading.Tasks;
using System.Windows;
using TableCloth.Resources;

namespace Spork.App.DependencyInjection;

/// <summary>
/// 진입점에서 <see cref="IHostApplicationBuilder"/>에 Spork 샌드박스 에이전트 모듈을
/// 합성하는 확장 메서드 모음. verb 기반 단일 바이너리 구조에서 spork verb 핸들러가
/// 이 메서드를 호출하여 모든 서비스/뷰모델/뷰를 DI에 등록한다.
/// </summary>
public static class UseSporkExtensions
{
    /// <summary>
    /// Spork 모듈의 모든 의존성을 빌더에 등록한다.
    /// </summary>
    public static IHostApplicationBuilder UseSpork(this IHostApplicationBuilder builder)
    {
        // Sentry SDK 초기화 + Serilog/Console 로그 싱크. 기존 진입점에서 그대로 옮긴 패턴
        // (using 블록의 즉시 dispose 거동은 Sentry SDK 글로벌 상태와 함께 검토 대상이나 본 Phase
        //  범위 외이므로 동작을 보존한다).
        using (var _ = SentrySdk.Init(o =>
        {
            o.Dsn = ConstantStrings.SentryDsn;
            o.Debug = true;
            o.TracesSampleRate = 1.0;
        }))
        {
            builder.Logging
                .AddSerilog(dispose: true)
                .AddConsole();
        }

        builder.Services.AddLogging();

        builder.Services.AddHttpClient(
            nameof(ConstantStrings.UserAgentText),
            c => c.DefaultRequestHeaders.Add("User-Agent", ConstantStrings.UserAgentText));
        builder.Services.AddHttpClient(
            nameof(ConstantStrings.FamiliarUserAgentText),
            c => c.DefaultRequestHeaders.Add("User-Agent", ConstantStrings.FamiliarUserAgentText));

        builder.Services
            .AddSingleton<IAppMessageBox, AppMessageBox>()
            .AddSingleton<IMessageBoxService, MessageBoxService>()
            .AddSingleton<IAppUserInterface, AppUserInterface>()
            .AddSingleton<ILicenseDescriptor, LicenseDescriptor>()
            .AddSingleton<IVisualThemeManager, VisualThemeManager>()
            .AddSingleton<ISharedLocations, SharedLocations>()
            .AddSingleton<IAppStartup, AppStartup>()
            .AddSingleton<IResourceResolver, ResourceResolver>()
            .AddSingleton<IResourceCacheManager, ResourceCacheManager>()
            .AddSingleton<ICommandLineArguments, CommandLineArguments>()
            .AddSingleton<IApplicationService, ApplicationService>()
            .AddSingleton<IUserDataStore, UserDataStore>()
            .AddSingleton<IX509CertScanner, X509CertScanner>()
            .AddSingleton<IShortcutCreator, ShortcutCreator>()
            .AddSingleton(_ => new TaskFactory(TaskScheduler.FromCurrentSynchronizationContext()));

        builder.Services
            .AddSingleton<IWebBrowserServiceFactory, WebBrowserServiceFactory>()
            .AddKeyedSingleton<IWebBrowserService, X86ChromiumEdgeWebBrowserService>(nameof(X86ChromiumEdgeWebBrowserService));

        builder.Services
            .AddSingleton<IStepsFactory, StepsFactory>()
            .AddSingleton<IStepsComposer, StepsComposer>()
            .AddSingleton<IStepsPlayer, StepsPlayer>()
            .AddKeyedSingleton<IStep, ConfigAhnLabSafeTransactionStep>(nameof(ConfigAhnLabSafeTransactionStep))
            .AddKeyedSingleton<IStep, DisableSmartAppControlStep>(nameof(DisableSmartAppControlStep))
            .AddKeyedSingleton<IStep, EdgeExtensionInstallStep>(nameof(EdgeExtensionInstallStep))
            .AddKeyedSingleton<IStep, OpenWebSiteStep>(nameof(OpenWebSiteStep))
            .AddKeyedSingleton<IStep, PackageInstallStep>(nameof(PackageInstallStep))
            .AddKeyedSingleton<IStep, PowerShellScriptRunStep>(nameof(PowerShellScriptRunStep))
            .AddKeyedSingleton<IStep, PrepareDirectoriesStep>(nameof(PrepareDirectoriesStep))
            .AddKeyedSingleton<IStep, ReloadEdgeStep>(nameof(ReloadEdgeStep));

        builder.Services
            .AddWindow<AboutWindow, AboutWindowViewModel>()
            .AddWindow<PrecautionsWindow, PrecautionsWindowViewModel>()
            .AddWindow<InstallStepsWindow, InstallStepsWindowViewModel>()
            .AddWindow<MainWindow, MainWindowViewModel>()
            .AddTransient<SiteReportWindow>()
            .AddSingleton<Application>(sp => new SporkApplication(sp.GetRequiredService<IHost>()));

        return builder;
    }
}
