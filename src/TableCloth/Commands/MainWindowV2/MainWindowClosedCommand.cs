using TableCloth.Components;

namespace TableCloth.Commands.MainWindow;

public sealed class MainWindowClosedCommand(
    ISandboxCleanupManager sandboxCleanupManager,
    IAppRestartManager appRestartManager) : CommandBase
{
    public override void Execute(object? parameter)
    {
        sandboxCleanupManager.TryCleanup();

        if (appRestartManager.IsRestartReserved())
            appRestartManager.RestartNow();
    }
}
