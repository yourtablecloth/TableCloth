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

// ctor에서 캐시한 서비스 필드(`?` 어노테이션)와 Host 속성 인식용. Spork.App 본체는 nullable
// 컨텍스트로 전환할 준비가 안 된 파일이 많아 annotations만 활성화.
#nullable enable annotations

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
        /// 호스트를 받아 SporkApplication을 생성. 필요한 서비스는 ctor에서 미리 캐시해 두므로
        /// Application_Startup이 동기/비동기 어느 경로로 발사되더라도 IServiceProvider가
        /// disposed된 상황에서 GetRequiredService를 다시 호출하지 않는다.
        /// </summary>
        [ActivatorUtilitiesConstructor]
        public SporkApplication(IHost host)
        {
            Host = host.EnsureArgumentNotNull("Host initialization not done.", nameof(host));
            this.InitServiceProvider(host.Services);

            // ctor 시점에 모든 의존성을 해소해 인스턴스 필드에 보관한다. WPF Application_Startup이
            // SafeFireAndForget 비동기로 흘러가도 이 필드들은 강참조라 항상 유효.
            var sp = host.Services;
            _appMessageBox = sp.GetRequiredService<IAppMessageBox>();
            _commandLineArguments = sp.GetRequiredService<ICommandLineArguments>();
            _appStartup = sp.GetRequiredService<IAppStartup>();
            _appUserInterface = sp.GetRequiredService<IAppUserInterface>();
            _logger = sp.GetRequiredService<ILogger<SporkApplication>>();

            InitializeComponent();

            SafeFireAndForgetExtensions.Initialize();
            SafeFireAndForgetExtensions.SetDefaultExceptionHandling((thrownException) =>
            {
                try
                {
                    _logger.LogError(thrownException, "Unexpected error occurred in fire-and-forget task.");
                }
                catch
                {
                    // 로거 자체가 비정상 상태인 경우 무시.
                }
            });
        }

        public IHost? Host { get; }

        private readonly IAppMessageBox? _appMessageBox;
        private readonly ICommandLineArguments? _commandLineArguments;
        private readonly IAppStartup? _appStartup;
        private readonly IAppUserInterface? _appUserInterface;
        private readonly ILogger<SporkApplication>? _logger;

        private async Task OnApplicationstartupAsync(object sender, StartupEventArgs e)
        {
            if (_appMessageBox == null || _commandLineArguments == null || _appStartup == null || _appUserInterface == null)
                throw new InvalidOperationException("SporkApplication was not constructed via the IHost-accepting constructor; cached services are missing.");

            var parsedArgs = _commandLineArguments.GetCurrent();

            if (parsedArgs.ShowCommandLineHelp)
            {
                _appMessageBox.DisplayInfo(await _commandLineArguments.GetHelpStringAsync(), MessageBoxButton.OK);
                return;
            }

            if (parsedArgs.ShowVersionHelp)
            {
                _appMessageBox.DisplayInfo(await _commandLineArguments.GetVersionStringAsync(), MessageBoxButton.OK);
                return;
            }

            var warnings = new List<string>();
            var result = await _appStartup.HasRequirementsMetAsync(warnings);

            if (!result.Succeed)
            {
                _appMessageBox.DisplayError(result.FailedReason, result.IsCritical);

                if (result.IsCritical)
                {
                    if (Helpers.IsDevelopmentBuild)
                        throw result.FailedReason ?? TableClothAppException.Issue();
                    else
                        Shutdown(-1);
                }
            }

            if (warnings.Any())
                _appMessageBox.DisplayError(string.Join(Environment.NewLine + Environment.NewLine, warnings), false);

            result = await _appStartup.InitializeAsync(warnings);

            if (!result.Succeed)
            {
                _appMessageBox.DisplayError(result.FailedReason, result.IsCritical);

                if (result.IsCritical)
                {
                    if (Helpers.IsDevelopmentBuild)
                        throw result.FailedReason ?? TableClothAppException.Issue();
                    else
                        Shutdown(-1);
                }
            }

            var mainWindow = _appUserInterface.CreateMainWindow();
            Current.MainWindow = mainWindow;
            mainWindow.Show();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
            => OnApplicationstartupAsync(sender, e).SafeFireAndForget();
    }
}
