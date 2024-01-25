using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Linq;
using System.Net;
using System.Windows;
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
using TableCloth.Components.Implementations;
using TableCloth.Dialogs;
using TableCloth.Events;
using TableCloth.Pages;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth;

public partial class App : Application
{
    public static IHostBuilder CreateHostBuilder(
        string[]? args = default,
        Action<IConfigurationBuilder>? configurationBuilderOverride = default,
        Action<ILoggingBuilder>? loggingBuilderOverride = default,
        Action<IServiceCollection>? servicesBuilderOverride = default)
    {
        if (args == null)
            args = Environment.GetCommandLineArgs().Skip(1).ToArray();

        return Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(ConfigureAppConfiguration + configurationBuilderOverride)
            .ConfigureLogging(ConfigureLogging + loggingBuilderOverride)
            .ConfigureServices(ConfigureServices + servicesBuilderOverride);
    }

    private static void ConfigureAppConfiguration(IConfigurationBuilder configure)
    {
    }

    private static void ConfigureLogging(ILoggingBuilder logging)
        => logging
            .AddSerilog(dispose: true)
            .AddConsole();

    private static void ConfigureServices(IServiceCollection services)
    {
        // Add HTTP Service
        services.AddHttpClient(
            nameof(ConstantStrings.UserAgentText),
            c => c.DefaultRequestHeaders.Add("User-Agent", ConstantStrings.UserAgentText));
        services.AddHttpClient(
            nameof(ConstantStrings.OldUserAgentText),
            c =>
            {
                c.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml");
                c.DefaultRequestHeaders.Add("User-Agent", ConstantStrings.OldUserAgentText);
            });

        // Add Components (Conditional)
        if (Helpers.IsAppxInstallation)
        {
            services
                .AddSingleton<IAppUpdateManager, MicrosoftStoreAppUpdateManager>();
        }
        else
        {
            services
                .AddSingleton<IAppUpdateManager, StandaloneAppUpdateManager>();
        }

        // Add Components
        services
            .AddSingleton<IAppUserInterface, AppUserInterface>()
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
            .AddSingleton<IShortcutCrerator, ShortcutCrerator>()
            .AddSingleton<ICommandLineArguments, CommandLineArguments>()
            .AddSingleton<IApplicationService, ApplicationService>()
            .AddSingleton<ICatalogDeserializer, CatalogDeserializer>();

        // Shared Commands
        services
            .AddSingleton<LaunchSandboxCommand>()
            .AddSingleton<CreateShortcutCommand>()
            .AddSingleton<CertSelectCommand>()
            .AddSingleton<AppRestartCommand>()
            .AddSingleton<CopyCommandLineCommand>()
            .AddSingleton<AboutThisAppCommand>()
            .AddSingleton<ShowDebugInfoCommand>();

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
            .AddSingleton<CertSelectWindowManualCertLoadCommand>()
            .AddSingleton<CertSelectWindowRequestConfirmCommand>()
            .AddSingleton<CertSelectWindowRequestCancelCommand>();

        // Main Window
        services
            .AddWindow<MainWindow, MainWindowViewModel>()
            .AddSingleton<MainWindowLoadedCommand>()
            .AddSingleton<MainWindowClosedCommand>();

        // Main Window v2
        services
            .AddWindow<MainWindowV2, MainWindowV2ViewModel>()
            .AddSingleton<MainWindowV2LoadedCommand>()
            .AddSingleton<MainWindowV2ClosedCommand>();

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

        // App
        services.AddTransient(_ => Current);
    }

    public App()
    {
        _host = CreateHostBuilder().Build();
        Current.InitServiceProvider(_host.Services);
        InitializeComponent();
    }

    private readonly IHost _host;
    private SplashScreen? _splashScreen;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var appUserInterface = _host.Services.GetRequiredService<IAppUserInterface>();
        _splashScreen = appUserInterface.CreateSplashScreen();
        _splashScreen.ViewModel.InitializeDone += ViewModel_InitializeDone;
        _splashScreen.Show();
    }

    private void ViewModel_InitializeDone(object? sender, DialogRequestEventArgs e)
    {
        if (_host == null)
            throw new Exception("Host initialization not done.");

        if (_splashScreen == null)
            return;

        _splashScreen.Hide();

        if (e.DialogResult.HasValue && e.DialogResult.Value)
        {
            var mainWindow = default(Window);
            if (_splashScreen.ViewModel.V2UIOptedIn)
                mainWindow = _host.Services.GetRequiredService<MainWindowV2>();
            else
                mainWindow = _host.Services.GetRequiredService<MainWindow>();

            this.MainWindow = mainWindow;
            mainWindow.Show();
        }

        _splashScreen.Close();
    }
}
