using Sentry;
using System;
using System.Linq;
using System.Windows;
using TableCloth.Components;
using TableCloth.Events;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.SplashScreen;

public sealed class SplashScreenLoadedCommand : ViewModelCommandBase<SplashScreenViewModel>
{
    public SplashScreenLoadedCommand(
        Application application,
        IAppStartup appStartup,
        IAppMessageBox appMessageBox,
        IPreferencesManager preferencesManager,
        IResourceCacheManager resourceCacheManager,
        IVisualThemeManager visualThemeManager,
        ICommandLineArguments commandLineArguments)
    {
        _application = application;
        _appStartup = appStartup;
        _appMessageBox = appMessageBox;
        _preferencesManager = preferencesManager;
        _resourceCacheManager = resourceCacheManager;
        _visualThemeManager = visualThemeManager;
        _commandLineArguments = commandLineArguments;
    }

    private readonly Application _application;
    private readonly IAppStartup _appStartup;
    private readonly IAppMessageBox _appMessageBox;
    private readonly IPreferencesManager _preferencesManager;
    private readonly IResourceCacheManager _resourceCacheManager;
    private readonly IVisualThemeManager _visualThemeManager;
    private readonly ICommandLineArguments _commandLineArguments;

    public override async void Execute(SplashScreenViewModel viewModel)
    {
        _visualThemeManager.ApplyAutoThemeChange(_application.MainWindow);

        try
        {
            viewModel.NotifyStatusUpdate(this, new StatusUpdateRequestEventArgs(
                StringResources.Status_ParsingCommandLine));

            var parsedArgs = _commandLineArguments.Current;

            if (parsedArgs != null && parsedArgs.ShowCommandLineHelp)
            {
                viewModel.AppStartupSucceed = false;
                _appMessageBox.DisplayInfo(StringResources.TableCloth_TableCloth_Switches_Help, MessageBoxButton.OK);
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

            var preferences = _preferencesManager.LoadPreferences();
            viewModel.V2UIOptedIn = preferences?.V2UIOptIn ?? true;
            viewModel.ParsedArgument = parsedArgs;

            viewModel.NotifyStatusUpdate(this, new StatusUpdateRequestEventArgs(
                StringResources.Status_CheckInternetConnection));

            if (!await _appStartup.CheckForInternetConnectionAsync())
                _appMessageBox.DisplayError(StringResources.Info_Offline, false);

            viewModel.NotifyStatusUpdate(this, new StatusUpdateRequestEventArgs(
                StringResources.Status_EvaluatingRequirementsMet));

            var result = await _appStartup.HasRequirementsMetAsync(viewModel.Warnings);

            if (!result.Succeed)
            {
                _appMessageBox.DisplayError(result.FailedReason, result.IsCritical);

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
                _appMessageBox.DisplayError(string.Join(Environment.NewLine + Environment.NewLine, viewModel.Warnings), false);

            viewModel.NotifyStatusUpdate(this, new StatusUpdateRequestEventArgs(
                StringResources.Status_InitializingApplication));

            result = await _appStartup.InitializeAsync(viewModel.Warnings);

            if (!result.Succeed)
            {
                _appMessageBox.DisplayError(result.FailedReason, result.IsCritical);

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

            await _resourceCacheManager.LoadCatalogDocumentAsync();

            viewModel.NotifyStatusUpdate(this, new StatusUpdateRequestEventArgs(
                StringResources.Status_LoadingImages));

            await _resourceCacheManager.LoadSiteImages();

            viewModel.AppStartupSucceed = true;
        }
        catch (Exception ex)
        {
            viewModel.AppStartupSucceed = false;
            viewModel.NotifyStatusUpdate(this, new StatusUpdateRequestEventArgs(
                StringResources.Status_InitializingFailed));
            _appMessageBox.DisplayError(ex, true);
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
