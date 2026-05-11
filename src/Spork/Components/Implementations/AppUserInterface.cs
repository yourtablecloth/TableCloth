using Microsoft.Extensions.DependencyInjection;
using Spork.Dialogs;
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

        public MainWindow CreateMainWindow()
            => _serviceProvider.GetRequiredService<MainWindow>();
    }
}
