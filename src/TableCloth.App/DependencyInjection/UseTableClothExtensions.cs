using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Threading.Tasks;
using System.Windows;
using TableCloth.Components;
using TableCloth.Components.Implementations;
using TableCloth.Dialogs;
using TableCloth.Pages;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.App.DependencyInjection;

/// <summary>
/// 진입점에서 <see cref="IHostApplicationBuilder"/>에 TableCloth 호스트 런처 모듈을
/// 합성하는 확장 메서드 모음. verb 기반 단일 바이너리 구조에서 TableCloth verb 핸들러가
/// 이 메서드를 호출하여 모든 서비스/뷰모델/뷰를 DI에 등록한다.
/// </summary>
public static class UseTableClothExtensions
{
    /// <summary>
    /// TableCloth 모듈의 모든 의존성을 빌더에 등록한다.
    /// </summary>
    public static IHostApplicationBuilder UseTableCloth(this IHostApplicationBuilder builder)
    {
        builder.Logging
            .AddSerilog(dispose: true)
            .AddConsole();

        builder.Services.AddLogging();

        builder.Services.AddHttpClient(
            nameof(ConstantStrings.UserAgentText),
            c => c.DefaultRequestHeaders.Add("User-Agent", ConstantStrings.UserAgentText));
        builder.Services.AddHttpClient(
            nameof(ConstantStrings.FamiliarUserAgentText),
            c => c.DefaultRequestHeaders.Add("User-Agent", ConstantStrings.FamiliarUserAgentText));
        builder.Services.AddHttpClient(
            nameof(StringResources.TableCloth_GitHubRestUAString),
            c => c.DefaultRequestHeaders.Add("User-Agent", StringResources.TableCloth_GitHubRestUAString));

        builder.Services
            .AddSingleton<IAppUserInterface, AppUserInterface>()
            .AddSingleton<IAppUpdateManager, AppUpdateManager>()
            .AddSingleton<ISharedLocations, SharedLocations>()
            .AddSingleton<IPreferencesManager, PreferencesManager>()
            .AddSingleton<IX509CertPairScanner, X509CertPairScanner>()
            .AddSingleton<IResourceCacheManager, ResourceCacheManager>()
            .AddSingleton<ISandboxBuilder, SandboxBuilder>()
            .AddSingleton<ISandboxLauncher, SandboxLauncher>()
            .AddSingleton<ISandboxCleanupManager, SandboxCleanupManager>()
            .AddSingleton<IAppStartup, AppStartup>()
            .AddSingleton<IResourceResolver, ResourceResolver>()
            .AddSingleton<ILicenseDescriptor, LicenseDescriptor>()
            .AddSingleton<IAppRestartManager, AppRestartManager>()
            .AddSingleton<ICommandLineComposer, CommandLineComposer>()
            .AddSingleton<IConfigurationComposer, ConfigurationComposer>()
            .AddSingleton<IVisualThemeManager, VisualThemeManager>()
            .AddSingleton<IAppMessageBox, AppMessageBox>()
            .AddSingleton<IMessageBoxService, MessageBoxService>()
            .AddSingleton<INavigationService, NavigationService>()
            .AddSingleton<IShortcutCreator, ShortcutCreator>()
            .AddSingleton<ICommandLineArguments, CommandLineArguments>()
            .AddSingleton<IApplicationService, ApplicationService>()
            .AddSingleton<IArchiveExpander, ArchiveExpander>()
            .AddSingleton<ICatalogDeserializer, CatalogDeserializer>()
            .AddSingleton(_ => new TaskFactory(TaskScheduler.FromCurrentSynchronizationContext()));

        builder.Services
            .AddWindow<DisclaimerWindow, DisclaimerWindowViewModel>()
            .AddWindow<InputPasswordWindow, InputPasswordWindowViewModel>()
            .AddWindow<AboutWindow, AboutWindowViewModel>()
            .AddWindow<OptionsWindow, OptionsWindowViewModel>()
            .AddWindow<CertSelectWindow, CertSelectWindowViewModel>()
            .AddWindow<MainWindow, MainWindowViewModel>()
            .AddPage<CatalogPage, CatalogPageViewModel>(addPageAsSingleton: true)
            .AddPage<DetailPage, DetailPageViewModel>()
            .AddPage<QuickStartPage, QuickStartPageViewModel>()
            .AddWindow<SplashScreen, SplashScreenViewModel>()
            .AddSingleton<Application>(sp => new TableClothApplication(sp.GetRequiredService<IHost>()));

        return builder;
    }
}
