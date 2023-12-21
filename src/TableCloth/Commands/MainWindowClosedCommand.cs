using TableCloth.Components;

namespace TableCloth.Commands
{
    public sealed class MainWindowClosedCommand : CommandBase
    {
        public MainWindowClosedCommand(
            SandboxCleanupManager sandboxCleanupManager,
            AppRestartManager appRestartManager)
        {
            _sandboxCleanupManager = sandboxCleanupManager;
            _appRestartManager = appRestartManager;
        }

        private readonly SandboxCleanupManager _sandboxCleanupManager;
        private readonly AppRestartManager _appRestartManager;

        public override void Execute(object? parameter)
        {
            _sandboxCleanupManager.TryCleanup();

            if (_appRestartManager.ReserveRestart)
                _appRestartManager.RestartNow();
        }
    }
}
