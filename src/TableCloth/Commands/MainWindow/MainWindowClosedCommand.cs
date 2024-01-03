using TableCloth.Components;

namespace TableCloth.Commands.MainWindow;

public sealed class MainWindowClosedCommand : CommandBase
{
    public MainWindowClosedCommand(
        SandboxCleanupManager sandboxCleanupManager,
        AppRestartManager appRestartManager)
    {
        _sandboxCleanupManager = sandboxCleanupManager;
    }

    private readonly SandboxCleanupManager _sandboxCleanupManager;

    public override void Execute(object? parameter)
    {
        _sandboxCleanupManager.TryCleanup();
    }
}
