using System;
using System.Diagnostics;
using TableCloth.Components;
using TableCloth.Resources;

namespace TableCloth.Commands;

public sealed class CheckUpdatedVersionCommand : CommandBase
{
    public CheckUpdatedVersionCommand(
        IResourceResolver resourceResolver,
        IAppMessageBox appMessageBox)
    {
        _resourceResolver = resourceResolver;
        _appMessageBox = appMessageBox;
    }

    private readonly IResourceResolver _resourceResolver;
    private readonly IAppMessageBox _appMessageBox;

    public override async void Execute(object? parameter)
    {
        try
        {
            var owner = "yourtablecloth";
            var repo = "TableCloth";
            var thisVersion = GetType().Assembly.GetName().Version;

            if (Version.TryParse(await _resourceResolver.GetLatestVersion(owner, repo), out var parsedVersion) &&
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
