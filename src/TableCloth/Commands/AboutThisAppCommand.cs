using TableCloth.Components;
using TableCloth.Dialogs;

namespace TableCloth.Commands;

public sealed class AboutThisAppCommand : CommandBase
{
    public AboutThisAppCommand(
        AppUserInterface appUserInterface)
    {
        _appUserInterface = appUserInterface;
    }

    private readonly AppUserInterface _appUserInterface;

    public override void Execute(object? parameter)
    {
        var aboutWindow = _appUserInterface.CreateWindow<AboutWindow>();
        aboutWindow.ShowDialog();
    }
}
