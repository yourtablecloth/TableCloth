using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Spork.Dialogs;
using Spork.ViewModels;
using System;
using System.Collections.Generic;

namespace Spork.Components.Implementations
{
    // 이슈 #296: Avalonia 는 Window.Owner 세터가 없어(소유는 ShowDialog(owner) 시점에 성립), WPF 시절의
    // SetOwnerIfAvailable 사전 설정을 제거하고 ShowDialog 시 소유자를 해석한다.
    public sealed class AppUserInterface : IAppUserInterface
    {
        public AppUserInterface(
            IServiceProvider serviceProvider,
            IApplicationService applicationService)
        {
            _serviceProvider = serviceProvider;
            _applicationService = applicationService;
        }

        private readonly IServiceProvider _serviceProvider;
        private readonly IApplicationService _applicationService;

        public AboutWindow CreateAboutWindow()
            => _serviceProvider.GetRequiredService<AboutWindow>();

        public PrecautionsWindow CreatePrecautionsWindow(IEnumerable<string> targetServiceIds = null)
        {
            var window = _serviceProvider.GetRequiredService<PrecautionsWindow>();
            window.ViewModel.TargetServiceIds = targetServiceIds;
            return window;
        }

        public SiteReportWindow CreateSiteReportWindow()
            => _serviceProvider.GetRequiredService<SiteReportWindow>();

        public InstallStepsWindow CreateInstallStepsWindow(IList<StepItemViewModel> steps, bool dryRun, string targetTitle = null, string targetIconKey = null)
        {
            var window = _serviceProvider.GetRequiredService<InstallStepsWindow>();
            window.ViewModel.InstallSteps = steps ?? new List<StepItemViewModel>();
            window.ViewModel.DryRun = dryRun;
            window.ViewModel.TargetTitle = targetTitle;
            window.ViewModel.TargetIconKey = targetIconKey;
            return window;
        }

        public MainWindow CreateMainWindow()
            => _serviceProvider.GetRequiredService<MainWindow>();

        public SplashScreen CreateSplashScreen()
            => _serviceProvider.GetRequiredService<SplashScreen>();

        public SandboxGuidanceWindow CreateSandboxGuidanceWindow()
            => _serviceProvider.GetRequiredService<SandboxGuidanceWindow>();

        public bool? ShowDialog(Window window)
        {
            var result = _applicationService.DispatchInvoke(new Func<bool?>(() =>
            {
                var owner = _applicationService.GetActiveWindow() ?? _applicationService.GetMainWindow();
                return DialogHost.ShowModal(window, owner);
            }), Array.Empty<object>());

            return result as bool?;
        }

        public bool ShowAhnLabSafeTxGuideDialog(string stSessPath)
        {
            // 스텝은 백그라운드 스레드에서 실행되므로, 창 생성/표시는 UI 스레드로 디스패치한다(AppMessageBox와 동일 방식).
            var result = _applicationService.DispatchInvoke(new Func<bool?>(() =>
            {
                var window = _serviceProvider.GetRequiredService<AhnLabSafeTxGuideWindow>();
                window.ViewModel.StSessPath = stSessPath;
                var owner = _applicationService.GetActiveWindow() ?? _applicationService.GetMainWindow();
                return DialogHost.ShowModal(window, owner);
            }), Array.Empty<object>());

            return result as bool? == true;
        }
    }
}
