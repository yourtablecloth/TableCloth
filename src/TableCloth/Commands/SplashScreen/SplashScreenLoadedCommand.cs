using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using Sentry;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TableCloth.Components;
using TableCloth.Events;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.SplashScreen;

public sealed class SplashScreenLoadedCommand(
    IApplicationService applicationService,
    IAppStartup appStartup,
    IAppMessageBox appMessageBox,
    IPreferencesManager preferencesManager,
    IResourceCacheManager resourceCacheManager,
    ICommandLineArguments commandLineArguments,
    IAppUpdateManager appUpdateManager) : ViewModelCommandBase<SplashScreenViewModel>, IAsyncCommand<SplashScreenViewModel>
{
    // 업데이트 관련 상태 메시지 (리소스 파일 생성 후 교체 필요)
    private const string StatusCheckingForUpdates = "Checking for updates...";
    private const string StatusDownloadingUpdate = "Downloading update ({0}%) - v{1}...";

    public override void Execute(SplashScreenViewModel viewModel)
        => ExecuteAsync(viewModel).SafeFireAndForget();

    public async Task ExecuteAsync(SplashScreenViewModel viewModel)
    {
        applicationService.ApplyCosmeticChangeToMainWindow();

        try
        {
            await viewModel.NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_ParsingCommandLine });

            var parsedArgs = commandLineArguments.GetCurrent();

            if (parsedArgs != null)
            {
                if (parsedArgs.ShowCommandLineHelp)
                {
                    viewModel.AppStartupSucceed = false;
                    appMessageBox.DisplayInfo(await commandLineArguments.GetHelpStringAsync(), MessageBoxButton.OK);
                    return;
                }

                if (parsedArgs.ShowVersionHelp)
                {
                    viewModel.AppStartupSucceed = false;
                    appMessageBox.DisplayInfo(await commandLineArguments.GetVersionStringAsync(), MessageBoxButton.OK);
                    return;
                }
            }

            await viewModel.NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_LoadingPreferences });

            var preferences = await preferencesManager.LoadPreferencesAsync();
            viewModel.ParsedArgument = parsedArgs;

            if (preferences?.UseLogCollection ?? true)
            {
                using var _ = SentrySdk.Init(o =>
                {
                    o.Dsn = ConstantStrings.SentryDsn;
                    o.Debug = true;
                    o.TracesSampleRate = 1.0;
                });
            }

            await viewModel.NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_CheckInternetConnection });

            if (!await appStartup.CheckForInternetConnectionAsync())
                appMessageBox.DisplayError(ErrorStrings.Error_Offline, false);

            // Velopack 자동 업데이트 체크 (Velopack으로 설치된 경우에만)
            if (appUpdateManager.IsInstalledViaVelopack)
            {
                await CheckAndApplyUpdatesAsync(viewModel);
            }

            await viewModel.NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_EvaluatingRequirementsMet });

            var result = await appStartup.HasRequirementsMetAsync(viewModel.Warnings);

            if (!result.Succeed)
            {
                appMessageBox.DisplayError(result.FailedReason, result.IsCritical);

                if (result.IsCritical)
                {
                    if (Helpers.IsDevelopmentBuild)
                        throw result.FailedReason ?? TableClothAppException.Issue();
                    else
                        applicationService.Shutdown(CodeResources.ExitCode_SystemError);
                }
            }

            if (viewModel.Warnings.Any())
                appMessageBox.DisplayError(string.Join(Environment.NewLine + Environment.NewLine, viewModel.Warnings), false);

            await viewModel.NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_InitializingApplication });

            result = await appStartup.InitializeAsync(viewModel.Warnings);

            if (!result.Succeed)
            {
                appMessageBox.DisplayError(result.FailedReason, result.IsCritical);

                if (result.IsCritical)
                {
                    if (Helpers.IsDevelopmentBuild)
                        throw result.FailedReason ?? TableClothAppException.Issue();
                    else
                        applicationService.Shutdown(CodeResources.ExitCode_SystemError);
                }
            }

            await viewModel.NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_LoadingCatalog });

            var doc = await resourceCacheManager.LoadCatalogDocumentAsync();

            await viewModel.NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_LoadingImages });

            await resourceCacheManager.LoadSiteImagesAsync();

            preferences?.Favorites?.ForEach(serviceId =>
            {
                var service = doc.Services.Find(x => x.Id == serviceId);
                if (service != null) service.IsFavorite = true;
            });

            viewModel.AppStartupSucceed = true;
        }
        catch (Exception ex)
        {
            viewModel.AppStartupSucceed = false;
            await viewModel.NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_InitializingFailed });
            appMessageBox.DisplayError(ex, true);
        }
        finally
        {
            if (viewModel.AppStartupSucceed)
            {
                await viewModel.NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_Done });
            }

            await viewModel.NotifyInitializedAsync(this, new DialogRequestEventArgs(viewModel.AppStartupSucceed));
        }
    }

    private async Task CheckAndApplyUpdatesAsync(SplashScreenViewModel viewModel)
    {
        try
        {
            await viewModel.NotifyStatusUpdateAsync(this, new() { Status = StatusCheckingForUpdates });

            var hasUpdate = await appUpdateManager.CheckForUpdatesAsync();

            if (hasUpdate)
            {
                viewModel.IsUpdating = true;
                viewModel.ShowUpdateProgress = true;

                var newVersion = appUpdateManager.AvailableVersion ?? "unknown";
                await viewModel.NotifyStatusUpdateAsync(this, new()
                {
                    Status = string.Format(StatusDownloadingUpdate, 0, newVersion)
                });

                var progress = new Progress<int>(percent =>
                {
                    viewModel.UpdateProgress = percent;
                    viewModel.Status = string.Format(StatusDownloadingUpdate, percent, newVersion);
                });

                await appUpdateManager.DownloadAndApplyUpdatesAsync(progress);
                // 앱이 재시작되므로 이후 코드는 실행되지 않음
            }
        }
        catch (Exception)
        {
            // 업데이트 실패는 무시하고 앱 실행 계속
            viewModel.IsUpdating = false;
            viewModel.ShowUpdateProgress = false;
        }
    }
}
