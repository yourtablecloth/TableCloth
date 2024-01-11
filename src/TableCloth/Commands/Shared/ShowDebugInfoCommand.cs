using System.Diagnostics;
using TableCloth.Components;
using TableCloth.Resources;

namespace TableCloth.Commands;

public sealed class ShowDebugInfoCommand : CommandBase
{
    public ShowDebugInfoCommand(
        ICommandLineArguments commandLineArguments,
        IAppMessageBox appMessageBox)
    {
        _commandLineArguments = commandLineArguments;
        _appMessageBox = appMessageBox;
    }

    private ICommandLineArguments _commandLineArguments;
    private IAppMessageBox _appMessageBox;

    public override void Execute(object? parameter)
    {
        _appMessageBox.DisplayInfo(StringResources.TableCloth_DebugInformation(
            Process.GetCurrentProcess().ProcessName,
            string.Join(" ", _commandLineArguments.Current.RawArguments),
            _commandLineArguments.Current.ToString(),
            Helpers.AppxPackageName)
        );
    }
}