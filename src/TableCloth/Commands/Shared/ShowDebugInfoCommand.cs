using System.Diagnostics;
using TableCloth.Components;
using TableCloth.Resources;

namespace TableCloth.Commands;

public sealed class ShowDebugInfoCommand(
    ICommandLineArguments commandLineArguments,
    IAppMessageBox appMessageBox) : CommandBase
{
    public override void Execute(object? parameter)
    {
        appMessageBox.DisplayInfo(StringResources.TableCloth_DebugInformation(
            Process.GetCurrentProcess().ProcessName,
            string.Join(" ", commandLineArguments.Current.RawArguments),
            commandLineArguments.Current.ToString())
        );
    }
}