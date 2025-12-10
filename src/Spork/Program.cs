using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentry;
using Serilog;
using Spork.Browsers;
using Spork.Browsers.Implementations;
using Spork.Commands.AboutWindow;
using Spork.Commands.MainWindow;
using Spork.Commands.PrecautionsWindow;
using Spork.Components;
using Spork.Components.Implementations;
using Spork.Dialogs;
using Spork.Steps;
using Spork.Steps.Implementations;
using Spork.ViewModels;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Windows;
using TableCloth;
using TableCloth.Models.Answers;
using TableCloth.Resources;

namespace Spork
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
            => RunApp(args);

        private static void SetDefaultCulture(CultureInfo desiredCulture)
        {
            Thread.CurrentThread.CurrentCulture = desiredCulture;
            Thread.CurrentThread.CurrentUICulture = desiredCulture;
            CultureInfo.DefaultThreadCurrentCulture = desiredCulture;
            CultureInfo.DefaultThreadCurrentUICulture = desiredCulture;
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static int RunApp(string[] args)
        {
            try
            {
                var answer = DeserializeSporkAnswersJson();

                if (!string.IsNullOrWhiteSpace(answer?.HostUILocale))
                {
                    var desiredCulture = new CultureInfo(answer.HostUILocale);
                    SetDefaultCulture(desiredCulture);
                }

                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                // Application.Current 속성은 아래 생성자를 호출하면서 자동으로 설정됩니다.
                var app = new App(CreateHostBuilder(args).Build());
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
                .AddSingleton<IApplicationService, ApplicationService>()
                .AddSingleton<IShortcutCreator, ShortcutCreator>();

            // Browser Services
            services
                .AddSingleton<IWebBrowserServiceFactory, WebBrowserServiceFactory>()
                .AddKeyedSingleton<IWebBrowserService, X86ChromiumEdgeWebBrowserService>(nameof(X86ChromiumEdgeWebBrowserService));

            // Steps
            services
                .AddSingleton<IStepsFactory, StepsFactory>()
                .AddSingleton<IStepsComposer, StepsComposer>()
                .AddSingleton<IStepsPlayer, StepsPlayer>()
                .AddKeyedSingleton<IStep, ConfigAhnLabSafeTransactionStep>(nameof(ConfigAhnLabSafeTransactionStep))
                .AddKeyedSingleton<IStep, EdgeExtensionInstallStep>(nameof(EdgeExtensionInstallStep))
                .AddKeyedSingleton<IStep, OpenWebSiteStep>(nameof(OpenWebSiteStep))
                .AddKeyedSingleton<IStep, PackageInstallStep>(nameof(PackageInstallStep))
                .AddKeyedSingleton<IStep, PowerShellScriptRunStep>(nameof(PowerShellScriptRunStep))
                .AddKeyedSingleton<IStep, PrepareDirectoriesStep>(nameof(PrepareDirectoriesStep))
                .AddKeyedSingleton<IStep, ReloadEdgeStep>(nameof(ReloadEdgeStep))
                .AddKeyedSingleton<IStep, SetDesktopWallpaperStep>(nameof(SetDesktopWallpaperStep))
                .AddKeyedSingleton<IStep, TryProtectCriticalServicesStep>(nameof(TryProtectCriticalServicesStep));

            // Shared Commands
            services
                .AddSingleton<OpenAppHomepageCommand>()
                .AddSingleton<AboutThisAppCommand>()
                .AddSingleton<ShowDebugInfoCommand>();

            // About Window
            services
                .AddWindow<AboutWindow, AboutWindowViewModel>()
                .AddSingleton<AboutWindowLoadedCommand>()
                .AddSingleton<AboutWindowCloseCommand>()
                .AddSingleton<OpenSponsorPageCommand>();

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

        internal static SporkAnswers DeserializeSporkAnswersJson()
        {
            try
            {
                // Get the directory where Spork.exe is located
                var exeDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var answerFilePath = Path.Combine(exeDirectory, "SporkAnswers.json");

                if (File.Exists(answerFilePath))
                {
                    using (var answerFileContent = File.OpenRead(answerFilePath))
                    {
                        return JsonSerializer.Deserialize<SporkAnswers>(answerFileContent);
                    }
                }

                return default;
            }
            catch { return default; }
        }
    }
}
