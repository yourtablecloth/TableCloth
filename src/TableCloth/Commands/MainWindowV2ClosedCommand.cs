using TableCloth.Components;

namespace TableCloth.Commands
{
    public sealed class MainWindowV2ClosedCommand : CommandBase
    {
        public MainWindowV2ClosedCommand(
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
            this.sandboxCleanupManager.TryCleanup();

            if (this.appRestartManager.ReserveRestart)
                this.appRestartManager.RestartNow();
        }
    }
}
