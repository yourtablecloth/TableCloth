using System;
using TableCloth.Components;
using TableCloth.ViewModels;

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
        if (parameter is not MainWindowV2ViewModel viewModel)
            throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

        _sandboxCleanupManager.TryCleanup();

        if (_appRestartManager.ReserveRestart)
            _appRestartManager.RestartNow();
    }
}
