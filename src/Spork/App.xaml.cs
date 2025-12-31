using AsyncAwaitBestPractices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spork.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TableCloth;

namespace Spork
{
    public partial class App : Application
    {
        internal App()
        {
            InitializeComponent();
        }

        public App(IHost host) : this()
        {
            SetupHost(host);
        }

        internal void SetupHost(IHost host)
        {
            Host = host.EnsureArgumentNotNull("Host initialization not done.", nameof(host));
            this.InitServiceProvider(host.Services);
        }

        public IHost Host { get; private set; }

        private async Task OnApplicationstartupAsync(object sender, StartupEventArgs e)
        {
            if (Host == null)
                throw new InvalidOperationException("Host is not initialized. Ensure SetupHost is called before application startup.");

            var appMessageBox = Host.Services.GetRequiredService<IAppMessageBox>();
            var commandLineArguments = Host.Services.GetRequiredService<ICommandLineArguments>();
            var parsedArgs = commandLineArguments.GetCurrent();

            if (parsedArgs.ShowCommandLineHelp)
            {
                appMessageBox.DisplayInfo(await commandLineArguments.GetHelpStringAsync(), MessageBoxButton.OK);
                return;
            }

            if (parsedArgs.ShowVersionHelp)
            {
                appMessageBox.DisplayInfo(await commandLineArguments.GetVersionStringAsync(), MessageBoxButton.OK);
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
                        throw result.FailedReason ?? TableClothAppException.Issue();
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
                        throw result.FailedReason ?? TableClothAppException.Issue();
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
