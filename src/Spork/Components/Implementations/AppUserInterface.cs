using Microsoft.Extensions.DependencyInjection;
using Spork.Dialogs;
using Spork.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows;

namespace Spork.Components.Implementations
{
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

        private TWindow SetOwnerIfAvailable<TWindow>(TWindow window) where TWindow : Window
        {
            var owner = _applicationService.GetActiveWindow() ?? _applicationService.GetMainWindow();

            if (owner != null && !ReferenceEquals(owner, window))
                window.Owner = owner;

            return window;
        }

        public AboutWindow CreateAboutWindow()
            => SetOwnerIfAvailable(_serviceProvider.GetRequiredService<AboutWindow>());

        public PrecautionsWindow CreatePrecautionsWindow(IEnumerable<string> targetServiceIds = null)
        {
            var window = SetOwnerIfAvailable(_serviceProvider.GetRequiredService<PrecautionsWindow>());
            window.ViewModel.TargetServiceIds = targetServiceIds;
            return window;
        }

        public SiteReportWindow CreateSiteReportWindow()
            => SetOwnerIfAvailable(_serviceProvider.GetRequiredService<SiteReportWindow>());

        public InstallStepsWindow CreateInstallStepsWindow(IList<StepItemViewModel> steps, bool dryRun, string targetTitle = null, string targetIconKey = null)
        {
            var window = SetOwnerIfAvailable(_serviceProvider.GetRequiredService<InstallStepsWindow>());
            window.ViewModel.InstallSteps = steps ?? new List<StepItemViewModel>();
            window.ViewModel.DryRun = dryRun;
            window.ViewModel.TargetTitle = targetTitle;
            window.ViewModel.TargetIconKey = targetIconKey;
            return window;
        }

        public MainWindow CreateMainWindow()
            => _serviceProvider.GetRequiredService<MainWindow>();
    }
}
