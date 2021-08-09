using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using TableCloth.Contracts;
using TableCloth.Implementations;
using TableCloth.Implementations.WPF;
using TableCloth.ViewModels;

namespace TableCloth
{
    internal static class Program
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        public static void Main(string[] args)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);

            ServiceProvider = services.BuildServiceProvider();
            var startup = ServiceProvider.GetService<IAppStartup>();
            var userInterface = ServiceProvider.GetService<IAppUserInterface>();
            var messageBox = ServiceProvider.GetService<IAppMessageBox>();

            startup.Arguments = args;

            if (!startup.HasRequirementsMet(out Exception failedReason, out bool isCritical))
            {
                messageBox.DisplayError(default, failedReason, isCritical);

                if (isCritical)
                {
                    Environment.Exit(1);
                    return;
                }
            }

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

        public static void ConfigureServices(ServiceCollection services)
        {
            // Add Logging
            services.AddLogging(c =>
            {
                c.AddSerilog(dispose: true);
            });

            // Add Services
            services.AddSingleton<IX509CertPairScanner, X509CertPairScanner>();
            services.AddSingleton<ICatalogDeserializer, CatalogDeserializer>();
            services.AddSingleton<ISandboxBuilder, SandboxBuilder>();
            services.AddSingleton<ISandboxLauncher, SandboxLauncher>();
            services.AddSingleton<IAppStartup, AppStartup>();

            // ViewModel
            services.AddSingleton<MainWindowViewModel>();
            services.AddTransient<CertSelectWindowViewModel>();

            // UI
            services.AddSingleton<IAppMessageBox, AppMessageBox>();
            services.AddSingleton<IAppUserInterface, AppUserInterface>();
        }
    }
}
