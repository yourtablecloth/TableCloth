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
            // Velopack 자동 업데이트 시도
            var hasUpdate = await appUpdateManager.CheckForUpdatesAsync();

            if (hasUpdate)
            {
                appMessageBox.DisplayInfo(InfoStrings.Info_UpdateRequired);
                await appUpdateManager.DownloadAndApplyUpdatesAsync();
                return;
            }

            // Velopack 설치가 아닌 경우 기존 방식으로 폴백
            var targetUrl = await appUpdateManager.QueryNewVersionDownloadUrlAsync();

            if (targetUrl.ThrownException != null)
            {
                appMessageBox.DisplayError(targetUrl.ThrownException, false);
                return;
            }

            if (!string.IsNullOrWhiteSpace(targetUrl.Result?.AbsoluteUri))
            {
                appMessageBox.DisplayInfo(InfoStrings.Info_UpdateRequired);
                var psi = new ProcessStartInfo(targetUrl.Result.AbsoluteUri) { UseShellExecute = true, };
                Process.Start(psi);
            }
            else
                appMessageBox.DisplayInfo(InfoStrings.Info_UpdateNotRequired);
        }
        catch (Exception ex)
        {
            appMessageBox.DisplayError(ex, false);
        }
    }
}
