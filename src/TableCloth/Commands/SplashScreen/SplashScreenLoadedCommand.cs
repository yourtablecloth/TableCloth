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
    Application application,
    IAppStartup appStartup,
    IAppMessageBox appMessageBox,
    IPreferencesManager preferencesManager,
    IResourceCacheManager resourceCacheManager,
    IVisualThemeManager visualThemeManager,
    ICommandLineArguments commandLineArguments) : ViewModelCommandBase<SplashScreenViewModel>
{
    public override async void Execute(SplashScreenViewModel viewModel)
    {
        visualThemeManager.ApplyAutoThemeChange(application.MainWindow);

        try
        {
            viewModel.NotifyStatusUpdate(this, new StatusUpdateRequestEventArgs(
                StringResources.Status_ParsingCommandLine));

            var parsedArgs = commandLineArguments.Current;

            if (parsedArgs != null && parsedArgs.ShowCommandLineHelp)
            {
                viewModel.AppStartupSucceed = false;
                appMessageBox.DisplayInfo(StringResources.TableCloth_TableCloth_Switches_Help, MessageBoxButton.OK);
                return;
            }

            viewModel.NotifyStatusUpdate(this, new StatusUpdateRequestEventArgs(
                StringResources.Status_InitSentrySDK));

            using var _ = SentrySdk.Init(o =>
            {
                o.Dsn = StringResources.SentryDsn;
                o.Debug = true;
                o.TracesSampleRate = 1.0;
            });

            viewModel.NotifyStatusUpdate(this, new StatusUpdateRequestEventArgs(
                StringResources.Status_LoadingPreferences));

            var preferences = preferencesManager.LoadPreferences();
            viewModel.V2UIOptedIn = preferences?.V2UIOptIn ?? true;
            viewModel.ParsedArgument = parsedArgs;

            viewModel.NotifyStatusUpdate(this, new StatusUpdateRequestEventArgs(
                StringResources.Status_CheckInternetConnection));

            if (!await appStartup.CheckForInternetConnectionAsync())
                appMessageBox.DisplayError(StringResources.Info_Offline, false);

            viewModel.NotifyStatusUpdate(this, new StatusUpdateRequestEventArgs(
                StringResources.Status_EvaluatingRequirementsMet));

            var result = await appStartup.HasRequirementsMetAsync(viewModel.Warnings);

            if (!result.Succeed)
            {
                appMessageBox.DisplayError(result.FailedReason, result.IsCritical);

                if (result.IsCritical)
                {
#if DEBUG
                    throw result.FailedReason ?? new Exception(StringResources.Error_Unknown());
#else
                    _application.Shutdown(-1);
#endif
                }
            }

            if (viewModel.Warnings.Any())
                appMessageBox.DisplayError(string.Join(Environment.NewLine + Environment.NewLine, viewModel.Warnings), false);

            viewModel.NotifyStatusUpdate(this, new StatusUpdateRequestEventArgs(
                StringResources.Status_InitializingApplication));

            result = await appStartup.InitializeAsync(viewModel.Warnings);

            if (!result.Succeed)
            {
                appMessageBox.DisplayError(result.FailedReason, result.IsCritical);

                if (result.IsCritical)
                {
#if DEBUG
                    throw result.FailedReason ?? new Exception(StringResources.Error_Unknown());
#else
                    _application.Shutdown(-1);
#endif
                }
            }

            viewModel.NotifyStatusUpdate(this, new StatusUpdateRequestEventArgs(
                StringResources.Status_LoadingCatalog));

            await resourceCacheManager.LoadCatalogDocumentAsync();

            viewModel.NotifyStatusUpdate(this, new StatusUpdateRequestEventArgs(
                StringResources.Status_LoadingImages));

            await resourceCacheManager.LoadSiteImages();

            viewModel.AppStartupSucceed = true;
        }
        catch (Exception ex)
        {
            viewModel.AppStartupSucceed = false;
            viewModel.NotifyStatusUpdate(this, new StatusUpdateRequestEventArgs(
                StringResources.Status_InitializingFailed));
            appMessageBox.DisplayError(ex, true);
        }
        finally
        {
            if (viewModel.AppStartupSucceed)
            {
                viewModel.NotifyStatusUpdate(this, new StatusUpdateRequestEventArgs(
                    StringResources.Status_Done));
            }

            viewModel.NotifyInitialized(this, new DialogRequestEventArgs(viewModel.AppStartupSucceed));
        }
    }
}
