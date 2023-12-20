using Microsoft.Extensions.DependencyInjection;
using Sentry;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TableCloth.Commands;
using TableCloth.Components;
using TableCloth.Pages;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth
{
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

            // Add Logging
            services.AddLogging(c =>
            {
                c.AddSerilog(dispose: true);
            });

            // Add Services
            services.AddSingleton<AppUserInterface>();
            services.AddSingleton<SharedLocations>();
            services.AddSingleton<PreferencesManager>();
            services.AddSingleton<X509CertPairScanner>();
            services.AddSingleton<CatalogDeserializer>();
            services.AddSingleton<CatalogCacheManager>();
            services.AddSingleton<SandboxBuilder>();
            services.AddSingleton<SandboxLauncher>();
            services.AddSingleton<SandboxCleanupManager>();
            services.AddSingleton<AppStartup>();
            services.AddSingleton<ResourceResolver>();
            services.AddSingleton<LicenseDescriptor>();
            services.AddSingleton<AppRestartManager>();
            services.AddSingleton<CommandLineParser>();
            services.AddSingleton<CommandLineComposer>();
            services.AddSingleton<VisualThemeManager>();
            services.AddSingleton<AppMessageBox>();
            services.AddSingleton<NavigationService>();

            // Commands
            services.AddSingleton<LaunchSandboxCommand>();
            services.AddSingleton<CreateShortcutCommand>();
            services.AddSingleton<CopyCommandLineCommand>();
            services.AddSingleton<CertSelectCommand>();
            services.AddSingleton<AppRestartCommand>();
            services.AddSingleton<AboutThisAppCommand>();
            services.AddSingleton<MainWindowLoadedCommand>();
            services.AddSingleton<MainWindowClosedCommand>();
            services.AddSingleton<MainWindowV2LoadedCommand>();
            services.AddSingleton<MainWindowV2ClosedCommand>();

            // UI
            services.AddTransient<DisclaimerWindow>();
            services.AddTransient<AboutWindow>();
            services.AddTransient<InputPasswordWindow>();

            services.AddTransient<CertSelectWindowViewModel>();
            services.AddTransient<CertSelectWindow>();

            services.AddTransient<MainWindowViewModel>(provider =>
            {
                return new MainWindowViewModel(
                    appUserInterface: provider.GetRequiredService<AppUserInterface>(),
                    catalogDeserializer: provider.GetRequiredService<CatalogDeserializer>(),
                    mainWindowLoadedCommand: provider.GetRequiredService<MainWindowLoadedCommand>(),
                    mainWindowClosedCommand: provider.GetRequiredService<MainWindowClosedCommand>(),
                    launchSandboxCommand: provider.GetRequiredService<LaunchSandboxCommand>(),
                    createShortcutCommand: provider.GetRequiredService<CreateShortcutCommand>(),
                    appRestartCommand: provider.GetRequiredService<AppRestartCommand>(),
                    aboutThisAppCommand: provider.GetRequiredService<AboutThisAppCommand>(),
                    certSelectCommand: provider.GetRequiredService<CertSelectCommand>());
            });
            services.AddSingleton<MainWindow>();

            services.AddTransient<CatalogPageViewModel>(provider =>
            {
                return new CatalogPageViewModel(
                    catalogCacheManager: provider.GetRequiredService<CatalogCacheManager>(),
                    navigationService: provider.GetRequiredService<NavigationService>(),
                    appRestartCommand: provider.GetRequiredService<AppRestartCommand>(),
                    aboutThisAppCommand: provider.GetRequiredService<AboutThisAppCommand>());
            });
            services.AddTransient<CatalogPage>();

            services.AddTransient<DetailPageViewModel>(provider =>
            {
                return new DetailPageViewModel(
                    appUserInterface: provider.GetRequiredService<AppUserInterface>(),
                    navigationService: provider.GetRequiredService<NavigationService>(),
                    sharedLocations: provider.GetRequiredService<SharedLocations>(),
                    certPairScanner: provider.GetRequiredService<X509CertPairScanner>(),
                    preferencesManager: provider.GetRequiredService<PreferencesManager>(),
                    appRestartManager: provider.GetRequiredService<AppRestartManager>(),
                    launchSandboxCommand: provider.GetRequiredService<LaunchSandboxCommand>(),
                    createShortcutCommand: provider.GetRequiredService<CreateShortcutCommand>(),
                    copyCommandLineCommand: provider.GetRequiredService<CopyCommandLineCommand>(),
                    certSelectCommand: provider.GetRequiredService<CertSelectCommand>());
            });
            services.AddTransient<DetailPage>();

            services.AddSingleton<MainWindowV2ViewModel>(provider =>
            {
                return new MainWindowV2ViewModel(
                    mainWindowV2LoadedCommand: provider.GetRequiredService<MainWindowV2LoadedCommand>(),
                    mainWindowV2ClosedCommand: provider.GetRequiredService<MainWindowV2ClosedCommand>());
            });
            services.AddSingleton<MainWindowV2>();

            // HTTP Request
            services.AddHttpClient(nameof(TableCloth), c =>
            {
                c.DefaultRequestHeaders.Add("User-Agent", StringResources.UserAgentText);
            });

            return services.BuildServiceProvider();
        }
    }
}
