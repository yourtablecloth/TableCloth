using TableCloth.Components;

namespace TableCloth.Commands;

public sealed class AboutThisAppCommand(
    IAppUserInterface appUserInterface) : CommandBase
{
    public override void Execute(object? parameter)
    {
        var aboutWindow = appUserInterface.CreateAboutWindow();
        aboutWindow.ShowDialog();
    }
}
