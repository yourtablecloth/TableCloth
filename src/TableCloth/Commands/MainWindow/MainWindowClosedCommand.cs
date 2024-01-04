using TableCloth.Components;

namespace TableCloth.Commands.MainWindow;

public sealed class MainWindowClosedCommand : CommandBase
{
    public MainWindowClosedCommand(
        ISandboxCleanupManager sandboxCleanupManager)
    {
        _sandboxCleanupManager = sandboxCleanupManager;
    }

    private readonly ISandboxCleanupManager _sandboxCleanupManager;

    public override void Execute(object? parameter)
    {
        _sandboxCleanupManager.TryCleanup();
    }
}
