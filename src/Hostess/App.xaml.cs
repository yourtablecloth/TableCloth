using Hostess.Commands;
using Hostess.Commands.AboutWindow;
using Hostess.Commands.MainWindow;
using Hostess.Commands.PrecautionsWindow;
using Hostess.Components;
using Hostess.Components.Implementations;
using Hostess.Dialogs;
using Hostess.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TableCloth;
using TableCloth.Resources;

namespace Hostess
{
    public partial class App : Application
    {
        public App()
        {
            _host = new HostBuilder()
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole();
                })
                .ConfigureServices(ConfigureServices)
                .Build();

            Current.InitServiceProvider(_host.Services);
            InitializeComponent();
        }

        private readonly IHost _host;

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            var appMessageBox = _host.Services.GetRequiredService<IAppMessageBox>();
            var commandLineArguments = _host.Services.GetRequiredService<ICommandLineArguments>();
            var parsedArgs = commandLineArguments.Current;

            if (parsedArgs.ShowCommandLineHelp)
            {
                appMessageBox.DisplayInfo(StringResources.TableCloth_Hostess_Switches_Help, MessageBoxButton.OK);
                return;
            }

            var appStartup = _host.Services.GetRequiredService<IAppStartup>();
            var appUserInterface = _host.Services.GetRequiredService<IAppUserInterface>();

            var warnings = new List<string>();
            var result = await appStartup.HasRequirementsMetAsync(warnings);

            if (!result.Succeed)
            {
                appMessageBox.DisplayError(result.FailedReason, result.IsCritical);

                if (result.IsCritical)
                {
                    if (Helpers.IsDevelopmentBuild)
                        throw result.FailedReason ?? new Exception(StringResources.Error_Unknown());
                    else
                        Shutdown(-1);
                }
            }

            if (warnings.Any())
                appMessageBox.DisplayError(string.Join(Environment.NewLine + Environment.NewLine, warnings), false);

            result = await appStartup.InitializeAsync(warnings);

            if (!result.Succeed)
            {
                appMessageBox.DisplayError(result.FailedReason, result.IsCritical);

                if (result.IsCritical)
                {
                    if (Helpers.IsDevelopmentBuild)
                        throw result.FailedReason ?? new Exception(StringResources.Error_Unknown());        
                    else
                        Shutdown(-1);
                }
            }

            var mainWindow = appUserInterface.CreateMainWindow();
            Current.MainWindow = mainWindow;
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Add HTTP Service
            services.AddHttpClient(
                nameof(ConstantStrings.UserAgentText),
                c => c.DefaultRequestHeaders.Add("User-Agent", ConstantStrings.UserAgentText));
            services.AddHttpClient(
                nameof(ConstantStrings.OldUserAgentText),
                c =>
                {
                    c.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml");
                    c.DefaultRequestHeaders.Add("User-Agent", ConstantStrings.OldUserAgentText);
                });

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
                .AddSingleton<MainWindowInstallPackagesCommand>();

            // InstallItem
            services
                .AddSingleton<ShowErrorMessageCommand>();

            // App
            services.AddTransient(_ => Current);
        }
    }
}
