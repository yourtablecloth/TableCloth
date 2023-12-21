using System;
using System.Diagnostics;
using TableCloth.Components;
using TableCloth.Resources;

namespace TableCloth.Commands.AboutWindow;

public sealed class CheckUpdatedVersionCommand : CommandBase
{
    public CheckUpdatedVersionCommand(
        ResourceResolver resourceResolver,
        AppMessageBox appMessageBox)
    {
        _resourceResolver = resourceResolver;
        _appMessageBox = appMessageBox;
    }

    private readonly ResourceResolver _resourceResolver;
    private readonly AppMessageBox _appMessageBox;

    public override async void Execute(object? parameter)
    {
        try
        {
            var owner = "yourtablecloth";
            var repo = "TableCloth";
            var thisVersion = GetType().Assembly.GetName().Version;

            if (Version.TryParse(await _resourceResolver.GetLatestVersion(owner, repo), out Version? parsedVersion) &&
                thisVersion != null && parsedVersion > thisVersion)
            {
                _appMessageBox.DisplayInfo(StringResources.Info_UpdateRequired);
                var targetUrl = await _resourceResolver.GetDownloadUrl(owner, repo);
                var psi = new ProcessStartInfo(targetUrl.AbsoluteUri) { UseShellExecute = true, };
                Process.Start(psi);
                return;
            }
        }
        catch { }

        _appMessageBox.DisplayInfo(StringResources.Info_UpdateNotRequired);
    }
}
