using TableCloth.Components;

namespace TableCloth.Commands.Shared;

public sealed class AppRestartCommand(
    IAppRestartManager appRestartManager) : CommandBase
{
    public override void Execute(object? parameter)
    {
        appRestartManager.RestartNow();
    }
}
