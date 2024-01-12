using System;
using System.Diagnostics;
using TableCloth.Components;
using TableCloth.Resources;

namespace TableCloth.Commands;

public sealed class CheckUpdatedVersionCommand(
    IAppUpdateManager appUpdateManager,
    IAppMessageBox appMessageBox) : CommandBase
{
    public override async void Execute(object? parameter)
    {
        var targetUrl = await appUpdateManager.QueryNewVersionDownloadUrl();

        if (!string.IsNullOrWhiteSpace(targetUrl))
        {
            appMessageBox.DisplayInfo(InfoStrings.Info_UpdateRequired);
            var psi = new ProcessStartInfo(targetUrl) { UseShellExecute = true, };
            Process.Start(psi);
        }
        else
            appMessageBox.DisplayInfo(InfoStrings.Info_UpdateNotRequired);
    }
}
