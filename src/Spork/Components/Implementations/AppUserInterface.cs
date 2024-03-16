using Spork.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Spork.Components.Implementations
{
    public sealed class AppUserInterface : IAppUserInterface
    {
        public AppUserInterface(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private readonly IServiceProvider _serviceProvider;

        public AboutWindow CreateAboutWindow()
            => _serviceProvider.GetRequiredService<AboutWindow>();

        public PrecautionsWindow CreatePrecautionsWindow()
            => _serviceProvider.GetRequiredService<PrecautionsWindow>();

        public MainWindow CreateMainWindow()
            => _serviceProvider.GetRequiredService<MainWindow>();
    }
}
