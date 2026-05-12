using AsyncAwaitBestPractices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spork.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TableCloth;

namespace Spork
{
    public partial class SporkApplication : Application
    {
        /// <summary>
        /// XAML 디자이너 호환용. 런타임에는 반드시 <see cref="SporkApplication(IHost)"/>를 사용한다.
        /// </summary>
        public SporkApplication()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 호스트를 받아 SporkApplication을 생성. Host 필드를 <c>InitializeComponent</c> 이전에 먼저
        /// 채워 두어 InitializeComponent 처리 중 어떤 경로로든 Application_Startup이 발사되어도
        /// Host == null 상태가 보이지 않게 한다.
        /// </summary>
        [ActivatorUtilitiesConstructor]
        public SporkApplication(IHost host)
        {
            Host = host.EnsureArgumentNotNull("Host initialization not done.", nameof(host));
            this.InitServiceProvider(host.Services);

            // WPF 이벤트 핸들러는 InitializeComponent 안에서 +=된다. Startup이 발사되기 전에 위에서
            // 필드/Properties를 모두 채워뒀으므로, 이후 Startup 시점엔 Host가 보장된다.
            InitializeComponent();

            SafeFireAndForgetExtensions.Initialize();
            SafeFireAndForgetExtensions.SetDefaultExceptionHandling((thrownException) =>
            {
                try
                {
                    var logger = host.Services.GetRequiredService<ILogger<SporkApplication>>();
                    logger.LogError(thrownException, "Unexpected error occurred in fire-and-forget task.");
                }
                catch
                {
                    // 로깅 시스템 자체가 아직 준비되지 않은 경로일 수 있으므로 무시.
                }
            });
        }

        public IHost? Host { get; }

        private async Task OnApplicationstartupAsync(object sender, StartupEventArgs e)
        {
            if (Host == null)
                throw new InvalidOperationException("Host is not initialized. Ensure SporkApplication was constructed via the IHost-accepting constructor.");

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
