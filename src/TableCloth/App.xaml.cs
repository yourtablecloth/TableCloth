using Microsoft.Extensions.DependencyInjection;
using Sentry;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TableCloth.Components;
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

        public IServiceProvider Services { get; }

        public IEnumerable<string> Arguments { get; set; } = new string[0];

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            using var _ = SentrySdk.Init(o =>
            {
                o.Dsn = StringResources.SentryDsn;
                o.Debug = true;
                o.TracesSampleRate = 1.0;
            });

            var startup = Services.GetService<AppStartup>()!;
            var userInterface = Services.GetService<AppUserInterface>()!;
            var messageBox = Services.GetService<AppMessageBox>()!;

            var args = Environment.GetCommandLineArgs().Skip(1);
            startup.Arguments = args;
            var warnings = new List<string>();

            if (!startup.HasRequirementsMet(warnings, out Exception failedReason, out bool isCritical))
            {
                messageBox.DisplayError(default, failedReason, isCritical);

                if (isCritical)
                {
                    Environment.Exit(1);
                    return;
                }
            }

            if (warnings.Any())
                messageBox.DisplayError(default, string.Join(Environment.NewLine + Environment.NewLine, warnings), false);

            if (!startup.Initialize(out failedReason, out isCritical))
            {
                messageBox.DisplayError(default, failedReason, isCritical);

                if (isCritical)
                {
                    Environment.Exit(2);
                    return;
                }
            }
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
            services.AddSingleton<Preferences>();
            services.AddSingleton<X509CertPairScanner>();
            services.AddSingleton<CatalogDeserializer>();
            services.AddSingleton<SandboxBuilder>();
            services.AddSingleton<SandboxLauncher>();
            services.AddSingleton<AppStartup>();
            services.AddSingleton<GitHubReleaseFinder>();
            services.AddSingleton<LicenseDescriptor>();

            // ViewModel
            services.AddSingleton<MainWindowViewModel>();
            services.AddTransient<CertSelectWindowViewModel>();
            services.AddSingleton<AboutWindowViewModel>();
            services.AddTransient<InputPasswordWindowViewModel>();

            // UI
            services.AddSingleton<AppMessageBox>();
            services.AddSingleton<AppUserInterface>();

            return services.BuildServiceProvider();
        }
    }
}
