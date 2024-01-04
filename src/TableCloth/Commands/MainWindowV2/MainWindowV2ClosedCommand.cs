using TableCloth.Components;

namespace TableCloth.Commands.MainWindowV2;

public sealed class MainWindowV2ClosedCommand : CommandBase
{
    public MainWindowV2ClosedCommand(
        ISandboxCleanupManager sandboxCleanupManager,
        IAppRestartManager appRestartManager)
    {
        _sandboxCleanupManager = sandboxCleanupManager;
        _appRestartManager = appRestartManager;
    }

    private readonly ISandboxCleanupManager _sandboxCleanupManager;
    private readonly IAppRestartManager _appRestartManager;

    public override void Execute(object? parameter)
    {
        _sandboxCleanupManager.TryCleanup();

        if (_appRestartManager.IsRestartReserved())
            _appRestartManager.RestartNow();
    }
}
