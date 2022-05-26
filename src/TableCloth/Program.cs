using Microsoft.Extensions.DependencyInjection;
using Sentry;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TableCloth.Contracts;
using TableCloth.Implementations;
using TableCloth.Resources;
using TableCloth.ViewModels;

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
                var startup = ServiceProvider.GetService<IAppStartup>()!;
                var userInterface = ServiceProvider.GetService<IAppUserInterface>()!;
                var messageBox = ServiceProvider.GetService<IAppMessageBox>()!;

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
            services.AddSingleton<ISharedLocations, SharedLocations>();
            services.AddSingleton<IPreferences, Preferences>();
            services.AddSingleton<IX509CertPairScanner, X509CertPairScanner>();
            services.AddSingleton<ICatalogDeserializer, CatalogDeserializer>();
            services.AddSingleton<ISandboxBuilder, SandboxBuilder>();
            services.AddSingleton<ISandboxLauncher, SandboxLauncher>();
            services.AddSingleton<IAppStartup, AppStartup>();

            // ViewModel
            services.AddSingleton<MainWindowViewModel>();
            services.AddTransient<CertSelectWindowViewModel>();
            services.AddSingleton<AboutWindowViewModel>();
            services.AddTransient<InputPasswordWindowViewModel>();

            // UI
            services.AddSingleton<IAppMessageBox, AppMessageBox>();
            services.AddSingleton<IAppUserInterface, AppUserInterface>();
        }
    }
}
