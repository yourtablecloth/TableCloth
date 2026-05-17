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
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TableCloth;
using TableCloth.Models.Answers;
using TableCloth.Resources;

#nullable enable

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
        // SporkAnswers.json은 호스트가 샌드박스 staging에 미리 떨궈둔 진입 파라미터 파일.
        // 본 메서드 진입 시점(WPF Application.Run 직전)에 로드해 스레드 컬처를 설정한다.
        ApplySporkAnswersIfPresent();

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

        // .NET Core/5+ 부터 HttpClient는 ServicePointManager 콜백을 무시하므로, net48 시절 Spork이
        // 가졌던 "서버 인증서 오류 시 사용자에게 안내 후 거절" 동작을 HttpClientHandler 레벨에서
        // 복원. 두 HttpClient 모두 동일한 검증 정책을 사용.
        builder.Services.AddHttpClient(
            nameof(ConstantStrings.UserAgentText),
            c => c.DefaultRequestHeaders.Add("User-Agent", ConstantStrings.UserAgentText))
            .ConfigurePrimaryHttpMessageHandler(CreateServerCertValidatingHandler);
        builder.Services.AddHttpClient(
            nameof(ConstantStrings.FamiliarUserAgentText),
            c => c.DefaultRequestHeaders.Add("User-Agent", ConstantStrings.FamiliarUserAgentText))
            .ConfigurePrimaryHttpMessageHandler(CreateServerCertValidatingHandler);

        builder.Services
            .AddSingleton<IAppMessageBox, AppMessageBox>()
            .AddSingleton<IMessageBoxService, MessageBoxService>()
            .AddSingleton<IAppUserInterface, AppUserInterface>()
            .AddSingleton<ILicenseDescriptor, LicenseDescriptor>()
            .AddSingleton<IVisualThemeManager, VisualThemeManager>()
            .AddSingleton<ISharedLocations, SharedLocations>()
            .AddSingleton<IAppStartup, AppStartup>()
            // 기본 등록은 noop. TableCloth.exe spork verb 핸들러는 직후에 builder.UseSandboxBootstrap()
            // (Spork.Sandbox 어셈블리)을 호출하여 실제 sandbox 구현으로 교체한다. 단독 Spork.exe는
            // 본 등록을 그대로 사용해 sandbox 전용 코드를 끌고 가지 않는다.
            .AddSingleton<ISandboxBootstrap, NoopSandboxBootstrap>()
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

    /// <summary>
    /// HttpClient 한 개에 대해 서버 인증서 검증 핸들러를 만든다. 검증 실패 시 IAppMessageBox로
    /// 사용자에게 알림을 띄우고 연결을 거절(false 반환). IAppMessageBox는 콜백 시점에 비로소
    /// IServiceProvider에서 해소하므로 DI 등록 순서와 무관하게 동작한다.
    /// </summary>
    private static HttpClientHandler CreateServerCertValidatingHandler(IServiceProvider sp)
    {
        return new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (request, cert, chain, errors) =>
            {
                if (errors == SslPolicyErrors.None)
                    return true;

                try
                {
                    var appMessageBox = sp.GetService<IAppMessageBox>();
                    if (appMessageBox != null && cert != null)
                    {
                        appMessageBox.DisplayError(
                            StringResources.Error_X509CertError(cert.Subject, errors.ToString()),
                            false);
                    }
                }
                catch
                {
                    // 알림 채널 자체가 비정상이어도 검증 결과는 false로 유지하여 안전한 쪽을 택한다.
                }
                return false;
            }
        };
    }

    private static void ApplySporkAnswersIfPresent()
    {
        SporkAnswers? answer = default;

        try
        {
            // Spork.exe(또는 통합된 TableCloth.exe spork)와 동일 디렉터리.
            // 단일 파일 게시에서도 안전한 AppContext.BaseDirectory 사용.
            var answerFilePath = Path.Combine(AppContext.BaseDirectory, "SporkAnswers.json");

            if (File.Exists(answerFilePath))
            {
                using var answerFileContent = File.OpenRead(answerFilePath);
                answer = JsonSerializer.Deserialize<SporkAnswers>(answerFileContent);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Spork] SporkAnswers.json load failed: {ex}");
            return;
        }

        if (string.IsNullOrWhiteSpace(answer?.HostUILocale))
            return;

        try
        {
            var desiredCulture = new CultureInfo(answer.HostUILocale);
            Thread.CurrentThread.CurrentCulture = desiredCulture;
            Thread.CurrentThread.CurrentUICulture = desiredCulture;
            CultureInfo.DefaultThreadCurrentCulture = desiredCulture;
            CultureInfo.DefaultThreadCurrentUICulture = desiredCulture;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Spork] Apply culture '{answer.HostUILocale}' failed: {ex}");
        }
    }
}
