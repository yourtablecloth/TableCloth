using Hostess.Commands;
using Hostess.Commands.AboutWindow;
using Hostess.Commands.MainWindow;
using Hostess.Commands.PrecautionsWindow;
using Hostess.Components;
using Hostess.Dialogs;
using Hostess.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TableCloth.Models;
using TableCloth.Resources;

namespace Hostess
{
    public partial class App : Application
    {
        public App()
        {
            Current.InitServiceProvider(_serviceProvider = ConfigureServices());
            InitializeComponent();
        }

        private readonly IServiceProvider _serviceProvider;
        
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var appMessageBox = _serviceProvider.GetRequiredService<AppMessageBox>();
            var appStartup = _serviceProvider.GetRequiredService<AppStartup>();
            var appUserInterface = _serviceProvider.GetRequiredService<AppUserInterface>();

            var warnings = new List<string>();
            var parsedArgs = CommandLineArgumentModel.ParseFromArgv();

            if (parsedArgs != null && parsedArgs.ShowCommandLineHelp)
            {
                appMessageBox.DisplayInfo(StringResources.TableCloth_Hostess_Switches_Help, MessageBoxButton.OK);
                return;
            }

            if (!appStartup.HasRequirementsMet(warnings, out Exception failedReason, out bool isCritical))
            {
                if (isCritical)
                    throw failedReason ?? new Exception(StringResources.Error_Unknown());

                appMessageBox.DisplayError(failedReason, isCritical);
            }

            if (warnings.Any())
                appMessageBox.DisplayError(string.Join(Environment.NewLine + Environment.NewLine, warnings), false);

            if (!appStartup.Initialize(out failedReason, out isCritical))
            {
                if (isCritical)
                    throw failedReason ?? new Exception(StringResources.Error_Unknown());

                appMessageBox.DisplayError(failedReason, isCritical);
            }

            var mainWindow = appUserInterface.CreateMainWindow();
            Current.MainWindow = mainWindow;
            mainWindow.Show();
        }

        private IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Add HTTP Service
            services.AddHttpClient(nameof(Hostess), c => c.DefaultRequestHeaders.Add("User-Agent", StringResources.UserAgentText));

            // Components
            services
                .AddSingleton<AppMessageBox>()
                .AddSingleton<AppUserInterface>()
                .AddSingleton<LicenseDescriptor>()
                .AddSingleton<ProtectCriticalServices>()
                .AddSingleton<SharedProperties>()
                .AddSingleton<VisualThemeManager>()
                .AddSingleton<SharedLocations>()
                .AddSingleton<AppStartup>();

            // Shared Commands
            services
                .AddSingleton<OpenAppHomepageCommand>()
                .AddSingleton<AboutThisAppCommand>();

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

            return services.BuildServiceProvider();
        }
    }
}
