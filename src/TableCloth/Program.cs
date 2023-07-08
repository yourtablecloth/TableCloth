using Microsoft.Extensions.DependencyInjection;
using Sentry;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TableCloth.Components;
using TableCloth.Models.ViewModels;
using TableCloth.Resources;

namespace TableCloth
{
    internal static class Program
    {
        public static IServiceProvider ServiceProvider { get; private set; } = default!;

        public static void Main(string[] args)
        {
            using (SentrySdk.Init(o =>
            {
                o.Dsn = StringResources.SentryDsn;
                o.Debug = true;
                o.TracesSampleRate = 1.0;
            }))
            {
                var services = new ServiceCollection();
                ConfigureServices(services);

                ServiceProvider = services.BuildServiceProvider();
                var startup = ServiceProvider.GetService<AppStartup>()!;
                var userInterface = ServiceProvider.GetService<AppUserInterface>()!;
                var messageBox = ServiceProvider.GetService<AppMessageBox>()!;

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

                userInterface.StartApplication(args);
            }
        }

        private static void ConfigureServices(IServiceCollection services)
        {
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
        }
    }
}
