using Sentry;
using System;
using System.Linq;
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
    ICommandLineArguments commandLineArguments) : ViewModelCommandBase<SplashScreenViewModel>
{
    // 뷰 모델과 연결된 이벤트 통지기를 호출할 때는 Dispatcher를 통해서 호출하도록 코드 수정이 필요함.
    public override async void Execute(SplashScreenViewModel viewModel)
    {
        applicationService.ApplyCosmeticChangeToMainWindow();

        try
        {
            await viewModel.NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_ParsingCommandLine });

            var parsedArgs = commandLineArguments.Current;

            if (parsedArgs != null && parsedArgs.ShowCommandLineHelp)
            {
                viewModel.AppStartupSucceed = false;
                appMessageBox.DisplayInfo(StringResources.TableCloth_TableCloth_Switches_Help, MessageBoxButton.OK);
                return;
            }

            await viewModel.NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_InitSentrySDK });

            using var _ = SentrySdk.Init(o =>
            {
                o.Dsn = ConstantStrings.SentryDsn;
                o.Debug = true;
                o.TracesSampleRate = 1.0;
            });

            await viewModel.NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_LoadingPreferences });

            var preferences = await preferencesManager.LoadPreferencesAsync();
            viewModel.V2UIOptedIn = preferences?.V2UIOptIn ?? true;
            viewModel.ParsedArgument = parsedArgs;

            await viewModel.NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_CheckInternetConnection });

            if (!await appStartup.CheckForInternetConnectionAsync())
                appMessageBox.DisplayError(ErrorStrings.Error_Offline, false);

            await viewModel.NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_EvaluatingRequirementsMet });

            var result = await appStartup.HasRequirementsMetAsync(viewModel.Warnings);

            if (!result.Succeed)
            {
                appMessageBox.DisplayError(result.FailedReason, result.IsCritical);

                if (result.IsCritical)
                {
                    if (Helpers.IsDevelopmentBuild)
                        throw result.FailedReason ?? new Exception(StringResources.Error_Unknown());
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
                        throw result.FailedReason ?? new Exception(StringResources.Error_Unknown());
                    else
                        applicationService.Shutdown(CodeResources.ExitCode_SystemError);
                }
            }

            await viewModel.NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_LoadingCatalog });

            await resourceCacheManager.LoadCatalogDocumentAsync();

            await viewModel.NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_LoadingImages });

            await resourceCacheManager.LoadSiteImagesAsync();

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
}
