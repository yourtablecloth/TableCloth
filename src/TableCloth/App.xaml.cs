using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Collections.Generic;
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
using TableCloth.Commands.SplashScreen;
using TableCloth.Components;
using TableCloth.Dialogs;
using TableCloth.Events;
using TableCloth.Pages;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth;

public partial class App : Application
{
    public App()
    {
        Services = ConfigureServices();

        InitializeComponent();
    }

    public new static App Current => (App)Application.Current;

    public IEnumerable<string> Arguments { get; private set; } = new string[] { };

    public IServiceProvider Services { get; }

    private SplashScreen? _splashScreen;

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        Arguments = e.Args;

        _splashScreen = Services.GetRequiredService<SplashScreen>();
        _splashScreen.ViewModel.InitializeDone += ViewModel_InitializeDone;
        _splashScreen.Show();
    }

    private void ViewModel_InitializeDone(object? sender, DialogRequestEventArgs e)
    {
        if (_splashScreen == null)
            return;

        _splashScreen.Hide();

        if (e.DialogResult.HasValue && e.DialogResult.Value)
        {
            var mainWindow = default(Window);
            if (_splashScreen.ViewModel.V2UIOptedIn)
                mainWindow = Services.GetRequiredService<MainWindowV2>();
            else
                mainWindow = Services.GetRequiredService<MainWindow>();

            this.MainWindow = mainWindow;
            mainWindow.Show();
        }

        _splashScreen.Close();
    }

    private IServiceProvider ConfigureServices()
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
            .AddSingleton<CatalogDeserializer>()
            .AddSingleton<CatalogCacheManager>()
            .AddSingleton<SandboxBuilder>()
            .AddSingleton<SandboxLauncher>()
            .AddSingleton<SandboxCleanupManager>()
            .AddSingleton<AppStartup>()
            .AddSingleton<ResourceResolver>()
            .AddSingleton<LicenseDescriptor>()
            .AddSingleton<AppRestartManager>()
            .AddSingleton<CommandLineParser>()
            .AddSingleton<CommandLineComposer>()
            .AddSingleton<ConfigurationComposer>()
            .AddSingleton<VisualThemeManager>()
            .AddSingleton<AppMessageBox>()
            .AddSingleton<NavigationService>()
            .AddSingleton<ShortcutCrerator>();

        // Shared Commands
        services
            .AddSingleton<LaunchSandboxCommand>()
            .AddSingleton<CreateShortcutCommand>()
            .AddSingleton<CertSelectCommand>()
            .AddSingleton<AppRestartCommand>()
            .AddSingleton<CopyCommandLineCommand>()
            .AddSingleton<AboutThisAppCommand>();

        // Disclaimer Window
        services.AddWindow<DisclaimerWindow, DisclaimerWindowViewModel>(
            viewModelImplementationFactory: provider => new(
                disclaimerWindowLoadedCommand: provider.GetRequiredService<DisclaimerWindowLoadedCommand>(),
                disclaimerWindowAcknowledgeCommand: provider.GetRequiredService<DisclaimerWindowAcknowledgeCommand>()
            ))
            .AddSingleton<DisclaimerWindowLoadedCommand>()
            .AddSingleton<DisclaimerWindowAcknowledgeCommand>();

        // Input Password Window
        services.AddWindow<InputPasswordWindow, InputPasswordWindowViewModel>(
            viewModelImplementationFactory: provider => new(
                inputPasswordWindowLoadedCommand: provider.GetRequiredService<InputPasswordWindowLoadedCommand>(),
                inputPasswordWindowConfirmCommand: provider.GetRequiredService<InputPasswordWindowConfirmCommand>(),
                inputPasswordWindowCancelCommand: provider.GetRequiredService<InputPasswordWindowCancelCommand>()
            ))
            .AddSingleton<InputPasswordWindowLoadedCommand>()
            .AddSingleton<InputPasswordWindowConfirmCommand>()
            .AddSingleton<InputPasswordWindowCancelCommand>();

        // About Window
        services.AddWindow<AboutWindow, AboutWindowViewModel>(
            viewModelImplementationFactory: provider => new(
                aboutWindowLoadedCommand: provider.GetRequiredService<AboutWindowLoadedCommand>(),
                openWebsiteCommand: provider.GetRequiredService<OpenWebsiteCommand>(),
                showSystemInfoCommand: provider.GetRequiredService<ShowSystemInfoCommand>(),
                checkUpdatedVersionCommand: provider.GetRequiredService<CheckUpdatedVersionCommand>(),
                openPrivacyPolicyCommand: provider.GetRequiredService<OpenPrivacyPolicyCommand>()
            ))
            .AddSingleton<AboutWindowLoadedCommand>()
            .AddSingleton<OpenWebsiteCommand>()
            .AddSingleton<ShowSystemInfoCommand>()
            .AddSingleton<CheckUpdatedVersionCommand>()
            .AddSingleton<OpenPrivacyPolicyCommand>();

        // Cert Select Window
        services.AddWindow<CertSelectWindow, CertSelectWindowViewModel>(
            viewModelImplementationFactory: provider =>
                new (
                    certSelectWindowScanCertPairCommand: provider.GetRequiredService<CertSelectWindowScanCertPairCommand>(),
                    certSelectWindowLoadedCommand: provider.GetRequiredService<CertSelectWindowLoadedCommand>(),
                    certSelectWindowManualCertLoadCommand: provider.GetRequiredService<CertSelectWindowManualCertLoadCommand>()
                ))
            .AddSingleton<CertSelectWindowScanCertPairCommand>()
            .AddSingleton<CertSelectWindowLoadedCommand>()
            .AddSingleton<CertSelectWindowManualCertLoadCommand>();

        // Main Window
        services.AddWindow<MainWindow, MainWindowViewModel>(
            viewModelImplementationFactory: provider => new(
                catalogDeserializer: provider.GetRequiredService<CatalogDeserializer>(),
                mainWindowLoadedCommand: provider.GetRequiredService<MainWindowLoadedCommand>(),
                mainWindowClosedCommand: provider.GetRequiredService<MainWindowClosedCommand>(),
                launchSandboxCommand: provider.GetRequiredService<LaunchSandboxCommand>(),
                createShortcutCommand: provider.GetRequiredService<CreateShortcutCommand>(),
                appRestartCommand: provider.GetRequiredService<AppRestartCommand>(),
                aboutThisAppCommand: provider.GetRequiredService<AboutThisAppCommand>(),
                certSelectCommand: provider.GetRequiredService<CertSelectCommand>()
            ))
            .AddSingleton<MainWindowLoadedCommand>()
            .AddSingleton<MainWindowClosedCommand>();

        // Main Window v2
        services.AddWindow<MainWindowV2, MainWindowV2ViewModel>(
            viewModelImplementationFactory: provider => new(
                mainWindowV2LoadedCommand: provider.GetRequiredService<MainWindowV2LoadedCommand>(),
                mainWindowV2ClosedCommand: provider.GetRequiredService<MainWindowV2ClosedCommand>()
            ))
            .AddSingleton<MainWindowV2LoadedCommand>()
            .AddSingleton<MainWindowV2ClosedCommand>();

        // Catalog Page
        services.AddPage<CatalogPage, CatalogPageViewModel>(
            viewModelImplementationFactory: provider => new CatalogPageViewModel(
                catalogCacheManager: provider.GetRequiredService<CatalogCacheManager>(),
                catalogPageLoadedCommand: provider.GetRequiredService<CatalogPageLoadedCommand>(),
                catalogPageItemSelectCommand: provider.GetRequiredService<CatalogPageItemSelectCommand>(),
                appRestartCommand: provider.GetRequiredService<AppRestartCommand>(),
                aboutThisAppCommand: provider.GetRequiredService<AboutThisAppCommand>()
            ))
            .AddSingleton<CatalogPageLoadedCommand>()
            .AddSingleton<CatalogPageItemSelectCommand>();

        // Detail Page
        services.AddPage<DetailPage, DetailPageViewModel>(
            viewModelImplementationFactory: provider => new DetailPageViewModel(
                detailPageLoadedCommand: provider.GetRequiredService<DetailPageLoadedCommand>(),
                detailPageSearchTextLostFocusCommand: provider.GetRequiredService<DetailPageSearchTextLostFocusCommand>(),
                detailPageGoBackCommand: provider.GetRequiredService<DetailPageGoBackCommand>(),
                detailPageOpenHomepageLinkCommand: provider.GetRequiredService<DetailPageOpenHomepageLinkCommand>(),
                launchSandboxCommand: provider.GetRequiredService<LaunchSandboxCommand>(),
                createShortcutCommand: provider.GetRequiredService<CreateShortcutCommand>(),
                copyCommandLineCommand: provider.GetRequiredService<CopyCommandLineCommand>(),
                certSelectCommand: provider.GetRequiredService<CertSelectCommand>()
            ))
            .AddSingleton<DetailPageLoadedCommand>()
            .AddSingleton<DetailPageSearchTextLostFocusCommand>()
            .AddSingleton<DetailPageGoBackCommand>()
            .AddSingleton<DetailPageOpenHomepageLinkCommand>();

        // Splash Screen
        services.AddWindow<SplashScreen, SplashScreenViewModel>(
            viewModelImplementationFactory: provider => new SplashScreenViewModel(
                splashScreenLoadedCommand: provider.GetRequiredService<SplashScreenLoadedCommand>()
            ))
            .AddSingleton<SplashScreenLoadedCommand>();

        return services.BuildServiceProvider();
    }
}
