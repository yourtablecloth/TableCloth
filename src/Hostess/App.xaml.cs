using Hostess.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TableCloth;
using TableCloth.Resources;
using AsyncAwaitBestPractices.MVVM;
using AsyncAwaitBestPractices;

namespace Hostess
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        internal void SetupHost(IHost host)
        {
            Host = host ?? throw new ArgumentException("Host initialization not done.", nameof(host));
            this.InitServiceProvider(host.Services);
        }

        public IHost Host { get; private set; }

        private async Task OnApplicationstartupAsync(object sender, StartupEventArgs e)
        {
            var appMessageBox = Host.Services.GetRequiredService<IAppMessageBox>();
            var commandLineArguments = Host.Services.GetRequiredService<ICommandLineArguments>();
            var parsedArgs = commandLineArguments.Current;

            if (parsedArgs.ShowCommandLineHelp)
            {
                appMessageBox.DisplayInfo(StringResources.TableCloth_Hostess_Switches_Help, MessageBoxButton.OK);
                return;
            }

            var appStartup = Host.Services.GetRequiredService<IAppStartup>();
            var appUserInterface = Host.Services.GetRequiredService<IAppUserInterface>();

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

        private void Application_Startup(object sender, StartupEventArgs e)
            => OnApplicationstartupAsync(sender, e).SafeFireAndForget();
    }
}
