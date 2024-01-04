using TableCloth.Components;

namespace TableCloth.Commands;

public sealed class AboutThisAppCommand : CommandBase
{
    public AboutThisAppCommand(
        IAppUserInterface appUserInterface)
    {
        _appUserInterface = appUserInterface;
    }

    private readonly IAppUserInterface _appUserInterface;

    public override void Execute(object? parameter)
    {
        var aboutWindow = _appUserInterface.CreateAboutWindow();
        aboutWindow.ShowDialog();
    }
}
