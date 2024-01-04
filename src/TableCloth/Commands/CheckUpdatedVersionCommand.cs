using System;
using System.Diagnostics;
using TableCloth.Components;
using TableCloth.Resources;

namespace TableCloth.Commands;

public sealed class CheckUpdatedVersionCommand(
    IResourceResolver resourceResolver,
    IAppMessageBox appMessageBox) : CommandBase
{
    public override async void Execute(object? parameter)
    {
        try
        {
            var owner = "yourtablecloth";
            var repo = "TableCloth";
            var thisVersion = GetType().Assembly.GetName().Version;

            if (Version.TryParse(await resourceResolver.GetLatestVersion(owner, repo), out var parsedVersion) &&
                thisVersion != null && parsedVersion > thisVersion)
            {
                appMessageBox.DisplayInfo(StringResources.Info_UpdateRequired);
                var targetUrl = await resourceResolver.GetDownloadUrl(owner, repo);
                var psi = new ProcessStartInfo(targetUrl.AbsoluteUri) { UseShellExecute = true, };
                Process.Start(psi);
                return;
            }
        }
        catch { }

        appMessageBox.DisplayInfo(StringResources.Info_UpdateNotRequired);
    }
}
