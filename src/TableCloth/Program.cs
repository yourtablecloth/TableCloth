using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Runtime.CompilerServices;
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
using TableCloth.Pages;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
        => RunApp(args);

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    private static int RunApp(string[] args)
    {
        try
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Application.Current 속성은 아래 생성자를 호출하면서 자동으로 설정됩니다.
            var app = new App();

            app.SetupHost(CreateHostBuilder(args).Build());
            app.Run();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex?.ToString() ?? "Unknown Error",
                "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        return Environment.ExitCode;
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            e.ExceptionObject?.ToString() ?? "Unknown Error",
            "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public static IHostBuilder CreateHostBuilder(
        string[]? args = default,
        Action<IConfigurationBuilder>? configurationBuilderOverride = default,
        Action<ILoggingBuilder>? loggingBuilderOverride = default,
        Action<IServiceCollection>? servicesBuilderOverride = default)
    {
        args ??= Helpers.GetCommandLineArguments();

        return Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(ConfigureAppConfiguration + configurationBuilderOverride)
            .ConfigureLogging(ConfigureLogging + loggingBuilderOverride)
            .ConfigureServices(ConfigureServices + servicesBuilderOverride);
    }

    private static void ConfigureAppConfiguration(IConfigurationBuilder configure)
    {
    }

    private static void ConfigureLogging(ILoggingBuilder logging)
    {
        logging
            .AddSerilog(dispose: true)
            .AddConsole();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Add Logging
        services.AddLogging();

        // Add HTTP Service
        services.AddHttpClient(
            nameof(ConstantStrings.UserAgentText),
            c => c.DefaultRequestHeaders.Add("User-Agent", ConstantStrings.UserAgentText));
        services.AddHttpClient(
            nameof(ConstantStrings.FamiliarUserAgentText),
            c => c.DefaultRequestHeaders.Add("User-Agent", ConstantStrings.FamiliarUserAgentText));

        // Add Components
        services
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
            .AddSingleton<IShortcutCrerator, ShortcutCrerator>()
            .AddSingleton<ICommandLineArguments, CommandLineArguments>()
            .AddSingleton<IApplicationService, ApplicationService>()
            .AddSingleton<IArchiveExpander, ArchiveExpander>()
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
            .AddSingleton<CatalogPageItemSelectCommand>()
            .AddSingleton<CatalogPageItemFavoriteCommand>();

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
        services.AddTransient(_ => Application.Current);
    }
}
