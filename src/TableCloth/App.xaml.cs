using Microsoft.Extensions.DependencyInjection;
using Sentry;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TableCloth.Commands;
using TableCloth.Components;
using TableCloth.Resources;
using TableCloth.ViewModels;
using Windows.Devices.WiFiDirect.Services;

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

            var startup = Services.GetService<AppStartup>()!;
            var messageBox = Services.GetService<AppMessageBox>()!;
            var commandLineParser = Services.GetService<CommandLineParser>();

            Arguments = e.Args;
            var warnings = new List<string>();

            var parsedArg = commandLineParser.Parse(e.Args);

            if (parsedArg.ShowCommandLineHelp)
            {
                messageBox.DisplayInfo(StringResources.TableCloth_TableCloth_Switches_Help, MessageBoxButton.OK);
                return;
            }

            if (!startup.HasRequirementsMet(warnings, out Exception failedReason, out bool isCritical))
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

            var preferencesManager = Services.GetService<PreferencesManager>();
            var preferences = preferencesManager.LoadPreferences();

            if (preferences.V2UIOptIn)
                StartupUri = new Uri("MainWindowV2.xaml", UriKind.Relative);
            else
                StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
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

            // ViewModel
            services.AddSingleton<MainWindowViewModel>();
            services.AddTransient<CertSelectWindowViewModel>();
            services.AddSingleton<AboutWindowViewModel>();
            services.AddTransient<InputPasswordWindowViewModel>();
            services.AddSingleton<MainWindowV2ViewModel>();
            services.AddSingleton<CatalogPageViewModel>();
            services.AddSingleton<DetailPageViewModel>();

            // Commands
            services.AddSingleton<LaunchSandboxCommand>();
            services.AddSingleton<CreateShortcutCommand>();
            services.AddSingleton<CopyCommandLineCommand>();
            services.AddSingleton<AppRestartCommand>();

            // UI
            services.AddSingleton<AppMessageBox>();

            // HTTP Request
            services.AddHttpClient(nameof(TableCloth), c =>
            {
                c.DefaultRequestHeaders.Add("User-Agent", StringResources.UserAgentText);
            });

            return services.BuildServiceProvider();
        }
    }
}
