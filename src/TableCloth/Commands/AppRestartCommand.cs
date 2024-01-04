using TableCloth.Components;

namespace TableCloth.Commands;

public sealed class AppRestartCommand : CommandBase
{
    public AppRestartCommand(
        IAppRestartManager appRestartManager)
    {
        _appRestartManager = appRestartManager;
    }

    private readonly IAppRestartManager _appRestartManager;

    public override void Execute(object? parameter)
    {
        _appRestartManager.RestartNow();
    }
}
