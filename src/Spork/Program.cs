using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sentry;
using Serilog;
using Spork.Browsers;
using Spork.Browsers.Implementations;
using Spork.Components;
using Spork.Components.Implementations;
using Spork.Dialogs;
using Spork.Steps;
using Spork.Steps.Implementations;
using Spork.ViewModels;
using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
        {
            try
            {
                var answer = default(SporkAnswers);

                try
                {
                    // Get the directory where Spork.exe is located
                    var exeDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    var answerFilePath = Path.Combine(exeDirectory, "SporkAnswers.json");

                    if (File.Exists(answerFilePath))
                    {
                        using (var answerFileContent = File.OpenRead(answerFilePath))
                        {
                            answer = JsonSerializer.Deserialize<SporkAnswers>(answerFileContent);
                        }
                    }
                }
                catch { answer = default; }

                if (!string.IsNullOrWhiteSpace(answer?.HostUILocale))
                {
                    var desiredCulture = new CultureInfo(answer.HostUILocale);
                    Thread.CurrentThread.CurrentCulture = desiredCulture;
                    Thread.CurrentThread.CurrentUICulture = desiredCulture;
                    CultureInfo.DefaultThreadCurrentCulture = desiredCulture;
                    CultureInfo.DefaultThreadCurrentUICulture = desiredCulture;
                }

                AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                {
                    MessageBox.Show(
                        e.ExceptionObject?.ToString() ?? "Unknown Error",
                        "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
                };

                if (args == null)
                    args = Helpers.GetCommandLineArguments();

                var builder = Host.CreateApplicationBuilder(args);

                // Add Logging
                builder.Services.AddLogging();

                // Add HTTP Service
                builder.Services.AddHttpClient(
                    nameof(ConstantStrings.UserAgentText),
                    c => c.DefaultRequestHeaders.Add("User-Agent", ConstantStrings.UserAgentText));
                builder.Services.AddHttpClient(
                    nameof(ConstantStrings.FamiliarUserAgentText),
                    c => c.DefaultRequestHeaders.Add("User-Agent", ConstantStrings.FamiliarUserAgentText));

                // Components
                builder.Services
                    .AddSingleton<IAppMessageBox, AppMessageBox>()
                    .AddSingleton<IMessageBoxService, MessageBoxService>()
                    .AddSingleton<IAppUserInterface, AppUserInterface>()
                    .AddSingleton<ILicenseDescriptor, LicenseDescriptor>()
                    .AddSingleton<IVisualThemeManager, VisualThemeManager>()
                    .AddSingleton<ISharedLocations, SharedLocations>()
                    .AddSingleton<IAppStartup, AppStartup>()
                    .AddSingleton<IResourceResolver, ResourceResolver>()
                    .AddSingleton<IResourceCacheManager, ResourceCacheManager>()
                    .AddSingleton<ICommandLineArguments, CommandLineArguments>()
                    .AddSingleton<IApplicationService, ApplicationService>()
                    .AddSingleton<IShortcutCreator, ShortcutCreator>()
                    .AddSingleton(_ => new TaskFactory(TaskScheduler.FromCurrentSynchronizationContext()));

                // Browser Services
                builder.Services
                    .AddSingleton<IWebBrowserServiceFactory, WebBrowserServiceFactory>()
                    .AddKeyedSingleton<IWebBrowserService, X86ChromiumEdgeWebBrowserService>(nameof(X86ChromiumEdgeWebBrowserService));

                // Steps
                builder.Services
                    .AddSingleton<IStepsFactory, StepsFactory>()
                    .AddSingleton<IStepsComposer, StepsComposer>()
                    .AddSingleton<IStepsPlayer, StepsPlayer>()
                    .AddKeyedSingleton<IStep, ConfigAhnLabSafeTransactionStep>(nameof(ConfigAhnLabSafeTransactionStep))
                    .AddKeyedSingleton<IStep, DisableSmartAppControlStep>(nameof(DisableSmartAppControlStep))
                    .AddKeyedSingleton<IStep, EdgeExtensionInstallStep>(nameof(EdgeExtensionInstallStep))
                    .AddKeyedSingleton<IStep, OpenWebSiteStep>(nameof(OpenWebSiteStep))
                    .AddKeyedSingleton<IStep, PackageInstallStep>(nameof(PackageInstallStep))
                    .AddKeyedSingleton<IStep, PowerShellScriptRunStep>(nameof(PowerShellScriptRunStep))
                    .AddKeyedSingleton<IStep, PrepareDirectoriesStep>(nameof(PrepareDirectoriesStep))
                    .AddKeyedSingleton<IStep, ReloadEdgeStep>(nameof(ReloadEdgeStep))
                    .AddKeyedSingleton<IStep, SetDesktopWallpaperStep>(nameof(SetDesktopWallpaperStep));

                // UI
                builder.Services
                    .AddWindow<AboutWindow, AboutWindowViewModel>()
                    .AddWindow<PrecautionsWindow, PrecautionsWindowViewModel>()
                    .AddWindow<MainWindow, MainWindowViewModel>()
                    .AddSingleton<Application>(sp => new App(sp.GetRequiredService<IHost>()));

                using (var _ = SentrySdk.Init(o =>
                {
                    o.Dsn = ConstantStrings.SentryDsn;
                    o.Debug = true;
                    o.TracesSampleRate = 1.0;
                }))
                {
                    builder.Logging
                        .AddSerilog(dispose: true)
                        .AddConsole();
                }

                using var appHost = builder.Build();
                appHost.Start();
                var app = appHost.Services.GetRequiredService<Application>();
                app.Run();
                appHost.StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex?.ToString() ?? "Unknown Error",
                    "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return Environment.ExitCode;
        }
    }
}
