using Microsoft.Extensions.DependencyInjection;
using Sentry;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TableCloth.Commands;
using TableCloth.Commands.AboutWindow;
using TableCloth.Commands.CertSelectWindow;
using TableCloth.Commands.DisclaimerWindow;
using TableCloth.Commands.InputPasswordWindow;
using TableCloth.Commands.MainWindow;
using TableCloth.Commands.MainWindowV2;
using TableCloth.Components;
using TableCloth.Dialogs;
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

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        using var _ = SentrySdk.Init(o =>
        {
            o.Dsn = StringResources.SentryDsn;
            o.Debug = true;
            o.TracesSampleRate = 1.0;
        });

        var startup = Services.GetRequiredService<AppStartup>()!;
        var messageBox = Services.GetRequiredService<AppMessageBox>()!;
        var commandLineParser = Services.GetRequiredService<CommandLineParser>();

        Arguments = e.Args;
        var warnings = new List<string>();

        var parsedArg = commandLineParser.Parse(e.Args);

        if (parsedArg.ShowCommandLineHelp)
        {
            messageBox.DisplayInfo(StringResources.TableCloth_TableCloth_Switches_Help, MessageBoxButton.OK);
            return;
        }

        if (!startup.HasRequirementsMet(warnings, out Exception? failedReason, out bool isCritical))
        {
            messageBox.DisplayError(failedReason, isCritical);

            if (isCritical)
            {
                Environment.Exit(1);
                return;
            }
        }

        if (warnings.Any())
            messageBox.DisplayError(string.Join(Environment.NewLine + Environment.NewLine, warnings), false);

        if (!startup.Initialize(out failedReason, out isCritical))
        {
            messageBox.DisplayError(failedReason, isCritical);

            if (isCritical)
            {
                Environment.Exit(2);
                return;
            }
        }

        var preferencesManager = Services.GetRequiredService<PreferencesManager>();
        var preferences = preferencesManager.LoadPreferences();
        var v2UIOptIn = preferences?.V2UIOptIn ?? true;
        var window = default(Window);

        if (v2UIOptIn)
            window = Services.GetRequiredService<MainWindowV2>();
        else
            window = Services.GetRequiredService<MainWindow>();

        this.MainWindow = window;
        window.Show();
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
                    scanCertPairCommand: provider.GetRequiredService<ScanCertPairCommand>(),
                    certSelectWindowLoadedCommand: provider.GetRequiredService<CertSelectWindowLoadedCommand>(),
                    manualCertLoadCommand: provider.GetRequiredService<ManualCertLoadCommand>()
                ))
            .AddSingleton<ScanCertPairCommand>()
            .AddSingleton<CertSelectWindowLoadedCommand>()
            .AddSingleton<ManualCertLoadCommand>();

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
                navigationService: provider.GetRequiredService<NavigationService>(),
                appRestartCommand: provider.GetRequiredService<AppRestartCommand>(),
                aboutThisAppCommand: provider.GetRequiredService<AboutThisAppCommand>()
            ));

        // Detail Page
        services.AddPage<DetailPage, DetailPageViewModel>(
            viewModelImplementationFactory: provider => new DetailPageViewModel(
                appUserInterface: provider.GetRequiredService<AppUserInterface>(),
                navigationService: provider.GetRequiredService<NavigationService>(),
                sharedLocations: provider.GetRequiredService<SharedLocations>(),
                certPairScanner: provider.GetRequiredService<X509CertPairScanner>(),
                preferencesManager: provider.GetRequiredService<PreferencesManager>(),
                appRestartManager: provider.GetRequiredService<AppRestartManager>(),
                launchSandboxCommand: provider.GetRequiredService<LaunchSandboxCommand>(),
                createShortcutCommand: provider.GetRequiredService<CreateShortcutCommand>(),
                copyCommandLineCommand: provider.GetRequiredService<CopyCommandLineCommand>(),
                certSelectCommand: provider.GetRequiredService<CertSelectCommand>()
            ));

        return services.BuildServiceProvider();
    }
}
