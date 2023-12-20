using System;
using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands
{
    public sealed class MainWindowClosedCommand : CommandBase
    {
        public MainWindowClosedCommand(
            SandboxCleanupManager sandboxCleanupManager,
            AppRestartManager appRestartManager)
        {
            this.sandboxCleanupManager = sandboxCleanupManager;
            this.appRestartManager = appRestartManager;
        }

        private readonly SandboxCleanupManager sandboxCleanupManager;
        private readonly AppRestartManager appRestartManager;

        public override void Execute(object? parameter)
        {
            if (parameter is not MainWindowViewModel viewModel)
                throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

            this.sandboxCleanupManager.TryCleanup();

            if (this.appRestartManager.ReserveRestart)
                this.appRestartManager.RestartNow();
        }
    }
}
