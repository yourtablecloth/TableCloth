using System;
using System.Diagnostics;
using System.IO;
using TableCloth.Components;
using TableCloth.Resources;

namespace TableCloth.Commands.AboutWindow;

public sealed class ShowSystemInfoCommand : CommandBase
{
    public ShowSystemInfoCommand(
        AppMessageBox appMessageBox)
    {
        _appMessageBox = appMessageBox;
    }

    private readonly AppMessageBox _appMessageBox;

    public override void Execute(object? parameter)
    {
        var msinfoPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "msinfo32.exe");

        if (!File.Exists(msinfoPath))
        {
            _appMessageBox.DisplayError(StringResources.Error_Cannot_Run_SysInfo, false);
            return;
        }

        var psi = new ProcessStartInfo(msinfoPath);
        Process.Start(psi);
    }
}
