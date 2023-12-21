using TableCloth.Components;

namespace TableCloth.Commands;

public sealed class AppRestartCommand : CommandBase
{
    public AppRestartCommand(
        AppRestartManager appRestartManager)
    {
        _appRestartManager = appRestartManager;
    }

    private readonly AppRestartManager _appRestartManager;

    public override void Execute(object? parameter)
    {
        _appRestartManager.RestartNow();
    }
}
