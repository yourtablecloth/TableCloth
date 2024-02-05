using Hostess.Commands.AboutWindow;
using Hostess.Commands.MainWindow;
using Hostess.Commands.PrecautionsWindow;
using Hostess.Components;
using Hostess.Components.Implementations;
using Hostess.Dialogs;
using Hostess.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentry;
using Serilog;
using System;
using System.Runtime.CompilerServices;
using System.Windows;
using TableCloth;
using TableCloth.Resources;

namespace Hostess
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
            => RunApp(args);

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void RunApp(string[] args)
        {
            // Application.Current 속성은 아래 생성자를 호출하면서 자동으로 설정됩니다.
            var app = new App();

            app.SetupHost(CreateHostBuilder(args).Build());
            app.Run();
        }

        public static IHostBuilder CreateHostBuilder(
            string[] args = default,
            Action<IConfigurationBuilder> configurationBuilderOverride = default,
            Action<ILoggingBuilder> loggingBuilderOverride = default,
            Action<IServiceCollection> servicesBuilderOverride = default)
        {
            if (args == null)
                args = Helpers.GetCommandLineArguments();

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
            using (var _ = SentrySdk.Init(o =>
            {
                o.Dsn = ConstantStrings.SentryDsn;
                o.Debug = true;
                o.TracesSampleRate = 1.0;
            }))
            {
                logging
                    .AddSerilog(dispose: true)
                    .AddConsole();
            }
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

            // Components
            services
                .AddSingleton<IAppMessageBox, AppMessageBox>()
                .AddSingleton<IMessageBoxService, MessageBoxService>()
                .AddSingleton<IAppUserInterface, AppUserInterface>()
                .AddSingleton<ILicenseDescriptor, LicenseDescriptor>()
                .AddSingleton<ICriticalServiceProtector, CriticalServiceProtector>()
                .AddSingleton<IVisualThemeManager, VisualThemeManager>()
                .AddSingleton<ISharedLocations, SharedLocations>()
                .AddSingleton<IAppStartup, AppStartup>()
                .AddSingleton<IResourceResolver, ResourceResolver>()
                .AddSingleton<IResourceCacheManager, ResourceCacheManager>()
                .AddSingleton<ICommandLineArguments, CommandLineArguments>()
                .AddSingleton<IStepsComposer, StepsComposer>()
                .AddSingleton<IStepsPlayer, StepsPlayer>()
                .AddSingleton<IApplicationService, ApplicationService>();

            // Shared Commands
            services
                .AddSingleton<OpenAppHomepageCommand>()
                .AddSingleton<AboutThisAppCommand>()
                .AddSingleton<ShowDebugInfoCommand>();

            // About Window
            services
                .AddWindow<AboutWindow, AboutWindowViewModel>()
                .AddSingleton<AboutWindowLoadedCommand>()
                .AddSingleton<AboutWindowCloseCommand>();

            // Precautions Window
            services
                .AddWindow<PrecautionsWindow, PrecautionsWindowViewModel>()
                .AddSingleton<PrecautionsWindowLoadedCommand>()
                .AddSingleton<PrecautionsWindowCloseCommand>();

            // Main Window
            services
                .AddWindow<MainWindow, MainWindowViewModel>()
                .AddSingleton<MainWindowLoadedCommand>()
                .AddSingleton<MainWindowInstallPackagesCommand>()
                .AddSingleton<ShowErrorMessageCommand>();

            // App
            services.AddTransient(_ => Application.Current);
        }
    }
}
