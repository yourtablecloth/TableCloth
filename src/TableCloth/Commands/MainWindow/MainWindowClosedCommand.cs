using TableCloth.Components;

namespace TableCloth.Commands.MainWindow;

public sealed class MainWindowClosedCommand(
    ISandboxCleanupManager sandboxCleanupManager) : CommandBase
{
    public override void Execute(object? parameter)
    {
        sandboxCleanupManager.TryCleanup();
    }
}
