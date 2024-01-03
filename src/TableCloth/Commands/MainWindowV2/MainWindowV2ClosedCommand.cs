using TableCloth.Components;

namespace TableCloth.Commands.MainWindowV2;

public sealed class MainWindowV2ClosedCommand : CommandBase
{
    public MainWindowV2ClosedCommand(
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
