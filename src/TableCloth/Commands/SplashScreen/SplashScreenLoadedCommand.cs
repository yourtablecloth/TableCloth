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

public sealed class SplashScreenLoadedCommand : CommandBase
{
    public SplashScreenLoadedCommand(
        AppStartup appStartup,
        AppMessageBox appMessageBox,
        CommandLineParser commandLineParser,
        PreferencesManager preferencesManager,
        ResourceCacheManager resourceCacheManager,
        VisualThemeManager visualThemeManager)
    {
        _appStartup = appStartup;
        _appMessageBox = appMessageBox;
        _commandLineParser = commandLineParser;
        _preferencesManager = preferencesManager;
        _resourceCacheManager = resourceCacheManager;
        _visualThemeManager = visualThemeManager;
    }

    private readonly AppStartup _appStartup;
    private readonly AppMessageBox _appMessageBox;
    private readonly CommandLineParser _commandLineParser;
    private readonly PreferencesManager _preferencesManager;
    private readonly ResourceCacheManager _resourceCacheManager;
    private readonly VisualThemeManager _visualThemeManager;

    public override async void Execute(object? parameter)
    {
        if (parameter is not SplashScreenViewModel viewModel)
            throw new ArgumentException("Selected paramter is not a compatible object.", nameof(parameter));

        _visualThemeManager.ApplyAutoThemeChange(
            Application.Current.MainWindow);

        try
        {
            viewModel.NotifyStatusUpdate(this, new StatusUpdateRequestEventArgs(
                StringResources.Status_ParsingCommandLine));

            var parsedArgs = _commandLineParser.ParseFromArgv();

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

            if (!await _appStartup.CheckForInternetConnection())
                _appMessageBox.DisplayError(StringResources.Info_Offline, false);

            viewModel.NotifyStatusUpdate(this, new StatusUpdateRequestEventArgs(
                StringResources.Status_EvaluatingRequirementsMet));

            if (!_appStartup.HasRequirementsMet(viewModel.Warnings, out Exception? failedReason, out bool isCritical))
            {
                if (isCritical)
                    throw failedReason ?? new Exception(StringResources.Error_Unknown());

                _appMessageBox.DisplayError(failedReason, isCritical);
            }

            if (viewModel.Warnings.Any())
                _appMessageBox.DisplayError(string.Join(Environment.NewLine + Environment.NewLine, viewModel.Warnings), false);

            viewModel.NotifyStatusUpdate(this, new StatusUpdateRequestEventArgs(
                StringResources.Status_InitializingApplication));

            if (!_appStartup.Initialize(out failedReason, out isCritical))
            {
                if (isCritical)
                    throw failedReason ?? new Exception(StringResources.Error_Unknown());

                _appMessageBox.DisplayError(failedReason, isCritical);
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
