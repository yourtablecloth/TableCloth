using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TableCloth.Commands;
using TableCloth.Commands.AboutWindow;
using TableCloth.Commands.CatalogPage;
using TableCloth.Commands.CertSelectWindow;
using TableCloth.Commands.DetailPage;
using TableCloth.Commands.DisclaimerWindow;
using TableCloth.Commands.InputPasswordWindow;
using TableCloth.Commands.MainWindow;
using TableCloth.Commands.MainWindowV2;
using TableCloth.Commands.Shared;
using TableCloth.Commands.SplashScreen;
using TableCloth.Components;
using TableCloth.Dialogs;
using TableCloth.Pages;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Test;

public sealed class ContainerFixture
{
    public ContainerFixture()
    {
        Services = ConfigureServices();
    }

    public IServiceProvider Services { get; private set; }

    private ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Add Logging Service
        services.AddLogging(c => c.AddSerilog(dispose: true));

        // Add HTTP Service
        services.AddHttpClient(nameof(TableCloth), c => c.DefaultRequestHeaders.Add("User-Agent", StringResources.UserAgentText));

        // Add Components
        services
            .AddSingleton<AppUserInterface>()
            .AddSingleton<SharedLocations>()
            .AddSingleton<PreferencesManager>()
            .AddSingleton<X509CertPairScanner>()
            .AddSingleton<ResourceCacheManager>()
            .AddSingleton<SandboxBuilder>()
            .AddSingleton<SandboxLauncher>()
            .AddSingleton<SandboxCleanupManager>()
            .AddSingleton<AppStartup>()
            .AddSingleton<ResourceResolver>()
            .AddSingleton<LicenseDescriptor>()
            .AddSingleton<AppRestartManager>()
            .AddSingleton<CommandLineComposer>()
            .AddSingleton<ConfigurationComposer>()
            .AddSingleton<VisualThemeManager>()
            .AddSingleton<AppMessageBox>()
            .AddSingleton<NavigationService>()
            .AddSingleton<ShortcutCrerator>()
            .AddSingleton<CommandLineArguments>();

        // Shared Commands
        services
            .AddSingleton<LaunchSandboxCommand>()
            .AddSingleton<CreateShortcutCommand>()
            .AddSingleton<CertSelectCommand>()
            .AddSingleton<AppRestartCommand>()
            .AddSingleton<CopyCommandLineCommand>()
            .AddSingleton<AboutThisAppCommand>();

        // Disclaimer Window
        services
            .AddWindow<DisclaimerWindow, DisclaimerWindowViewModel>()
            .AddSingleton<DisclaimerWindowLoadedCommand>()
            .AddSingleton<DisclaimerWindowAcknowledgeCommand>();

        // Input Password Window
        services
            .AddWindow<InputPasswordWindow, InputPasswordWindowViewModel>()
            .AddSingleton<InputPasswordWindowLoadedCommand>()
            .AddSingleton<InputPasswordWindowConfirmCommand>()
            .AddSingleton<InputPasswordWindowCancelCommand>();

        // About Window
        services
            .AddWindow<AboutWindow, AboutWindowViewModel>()
            .AddSingleton<AboutWindowLoadedCommand>()
            .AddSingleton<OpenWebsiteCommand>()
            .AddSingleton<ShowSystemInfoCommand>()
            .AddSingleton<CheckUpdatedVersionCommand>()
            .AddSingleton<OpenPrivacyPolicyCommand>();

        // Cert Select Window
        services
            .AddWindow<CertSelectWindow, CertSelectWindowViewModel>()
            .AddSingleton<CertSelectWindowScanCertPairCommand>()
            .AddSingleton<CertSelectWindowLoadedCommand>()
            .AddSingleton<CertSelectWindowManualCertLoadCommand>();

        // Main Window
        services
            .AddWindow<MainWindow, MainWindowViewModel>()
            .AddSingleton<MainWindowLoadedCommand>()
            .AddSingleton<MainWindowClosedCommand>();

        // Main Window v2
        services
            .AddWindow<MainWindowV2, MainWindowV2ViewModel>()
            .AddSingleton<MainWindowV2LoadedCommand>()
            .AddSingleton<MainWindowClosedCommand>();

        // Catalog Page
        services
            .AddPage<CatalogPage, CatalogPageViewModel>(addPageAsSingleton: true)
            .AddSingleton<CatalogPageLoadedCommand>()
            .AddSingleton<CatalogPageItemSelectCommand>();

        // Detail Page
        services
            .AddPage<DetailPage, DetailPageViewModel>()
            .AddSingleton<DetailPageLoadedCommand>()
            .AddSingleton<DetailPageSearchTextLostFocusCommand>()
            .AddSingleton<DetailPageGoBackCommand>()
            .AddSingleton<DetailPageOpenHomepageLinkCommand>();

        // Splash Screen
        services
            .AddWindow<SplashScreen, SplashScreenViewModel>()
            .AddSingleton<SplashScreenLoadedCommand>();

        return services.BuildServiceProvider();
    }
}
