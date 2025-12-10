using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TableCloth.Components;
using TableCloth.Resources;

namespace TableCloth.Commands.AboutWindow;

public sealed class CheckUpdatedVersionCommand(
    IAppUpdateManager appUpdateManager,
    IAppMessageBox appMessageBox) : CommandBase, IAsyncCommand<object?>
{
    public override void Execute(object? parameter)
        => ExecuteAsync(parameter).SafeFireAndForget();

    public async Task ExecuteAsync(object? _)
    {
        try
        {
            // Velopack으로 설치된 경우 자동 업데이트
            if (appUpdateManager.IsInstalledViaVelopack)
            {
                var hasUpdate = await appUpdateManager.CheckForUpdatesAsync();

                if (hasUpdate)
                {
                    appMessageBox.DisplayInfo(InfoStrings.Info_UpdateRequired);
                    await appUpdateManager.DownloadAndApplyUpdatesAsync();
                    return;
                }

                appMessageBox.DisplayInfo(InfoStrings.Info_UpdateNotRequired);
                return;
            }

            // Velopack으로 설치되지 않은 경우 GitHub Releases 페이지로 안내
            var releasesUrl = appUpdateManager.GetReleasesPageUrl();
            var psi = new ProcessStartInfo(releasesUrl.AbsoluteUri) { UseShellExecute = true };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            appMessageBox.DisplayError(ex, false);
        }
    }
}
