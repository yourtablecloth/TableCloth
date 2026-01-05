using Microsoft.Extensions.DependencyInjection;
using Spork.Dialogs;
using System;
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

        public PrecautionsWindow CreatePrecautionsWindow()
            => SetOwnerIfAvailable(_serviceProvider.GetRequiredService<PrecautionsWindow>());

        public MainWindow CreateMainWindow()
            => _serviceProvider.GetRequiredService<MainWindow>();
    }
}
