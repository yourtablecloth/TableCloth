using TableCloth.Components;

namespace TableCloth.Commands.MainWindowV2;

public sealed class MainWindowV2ClosedCommand(
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
