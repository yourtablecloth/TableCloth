using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sentry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TableCloth.Components;
using TableCloth.Events;
using TableCloth.Models;
using TableCloth.Resources;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public partial class SplashScreenViewModelForDesigner : SplashScreenViewModel { }

public partial class SplashScreenViewModel : ViewModelBase
{
    protected SplashScreenViewModel() { }

    public SplashScreenViewModel(
        IApplicationService applicationService,
        IAppStartup appStartup,
        IAppMessageBox appMessageBox,
        IPreferencesManager preferencesManager,
        IResourceCacheManager resourceCacheManager,
        ICommandLineArguments commandLineArguments,
        IAppUpdateManager appUpdateManager)
    {
        _applicationService = applicationService;
        _appStartup = appStartup;
        _appMessageBox = appMessageBox;
        _preferencesManager = preferencesManager;
        _resourceCacheManager = resourceCacheManager;
        _commandLineArguments = commandLineArguments;
        _appUpdateManager = appUpdateManager;
    }

    public event EventHandler<StatusUpdateRequestEventArgs>? StatusUpdate;
    public event EventHandler<DialogRequestEventArgs>? InitializeDone;

    public async Task NotifyStatusUpdateAsync(object sender, StatusUpdateRequestEventArgs e, CancellationToken cancellationToken = default)
        => await TaskFactory.StartNew(() => StatusUpdate?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    public async Task NotifyInitializedAsync(object sender, DialogRequestEventArgs e, CancellationToken cancellationToken = default)
        => await TaskFactory.StartNew(() => InitializeDone?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    [RelayCommand]
    private async Task SplashScreenLoaded()
    {
        _applicationService.ApplyCosmeticChangeToMainWindow();

        try
        {
            await NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_ParsingCommandLine });

            var parsedArgs = _commandLineArguments.GetCurrent();

            if (parsedArgs != null)
            {
                if (parsedArgs.ShowCommandLineHelp)
                {
                    AppStartupSucceed = false;
                    _appMessageBox.DisplayInfo(await _commandLineArguments.GetHelpStringAsync(), MessageBoxButton.OK);
                    return;
                }

                if (parsedArgs.ShowVersionHelp)
                {
                    AppStartupSucceed = false;
                    _appMessageBox.DisplayInfo(await _commandLineArguments.GetVersionStringAsync(), MessageBoxButton.OK);
                    return;
                }
            }

            await NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_LoadingPreferences });

            var preferences = await _preferencesManager.LoadPreferencesAsync();
            ParsedArgument = parsedArgs;

            if (preferences?.UseLogCollection ?? true)
            {
                using var _ = SentrySdk.Init(o =>
                {
                    o.Dsn = ConstantStrings.SentryDsn;
                    o.Debug = true;
                    o.TracesSampleRate = 1.0;
                });
            }

            await NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_CheckInternetConnection });

            if (!await _appStartup.CheckForInternetConnectionAsync())
                _appMessageBox.DisplayError(ErrorStrings.Error_Offline, false);

            // Velopack 자동 업데이트 체크 (Velopack으로 설치된 경우에만)
            if (_appUpdateManager.IsInstalledViaVelopack)
            {
                await CheckAndApplyUpdatesAsync();
            }

            await NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_EvaluatingRequirementsMet });

            var result = await _appStartup.HasRequirementsMetAsync(Warnings);

            if (!result.Succeed)
            {
                _appMessageBox.DisplayError(result.FailedReason, result.IsCritical);

                if (result.IsCritical)
                {
                    if (Helpers.IsDevelopmentBuild)
                        throw result.FailedReason ?? TableClothAppException.Issue();
                    else
                        _applicationService.Shutdown(CodeResources.ExitCode_SystemError);
                }
            }

            if (Warnings.Any())
                _appMessageBox.DisplayError(string.Join(Environment.NewLine + Environment.NewLine, Warnings), false);

            await NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_InitializingApplication });

            result = await _appStartup.InitializeAsync(Warnings);

            if (!result.Succeed)
            {
                _appMessageBox.DisplayError(result.FailedReason, result.IsCritical);

                if (result.IsCritical)
                {
                    if (Helpers.IsDevelopmentBuild)
                        throw result.FailedReason ?? TableClothAppException.Issue();
                    else
                        _applicationService.Shutdown(CodeResources.ExitCode_SystemError);
                }
            }

            await NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_LoadingCatalog });

            var doc = await _resourceCacheManager.LoadCatalogDocumentAsync();

            await NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_LoadingImages });

            await _resourceCacheManager.LoadSiteImagesAsync();

            preferences?.Favorites?.ForEach(serviceId =>
            {
                var service = doc.Services.Find(x => x.Id == serviceId);
                if (service != null) service.IsFavorite = true;
            });

            AppStartupSucceed = true;
        }
        catch (Exception ex)
        {
            AppStartupSucceed = false;
            await NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_InitializingFailed });
            _appMessageBox.DisplayError(ex, true);
        }
        finally
        {
            if (AppStartupSucceed)
            {
                await NotifyStatusUpdateAsync(this, new() { Status = UIStringResources.Status_Done });
            }

            await NotifyInitializedAsync(this, new DialogRequestEventArgs(AppStartupSucceed));
        }
    }

    private async Task CheckAndApplyUpdatesAsync()
    {
        // 업데이트 관련 상태 메시지 (리소스 파일 생성 후 교체 필요)
        const string StatusCheckingForUpdates = "Checking for updates...";
        const string StatusDownloadingUpdate = "Downloading update ({0}%) - v{1}...";

        try
        {
            await NotifyStatusUpdateAsync(this, new() { Status = StatusCheckingForUpdates });

            var hasUpdate = await _appUpdateManager.CheckForUpdatesAsync();

            if (hasUpdate)
            {
                IsUpdating = true;
                ShowUpdateProgress = true;

                var newVersion = _appUpdateManager.AvailableVersion ?? "unknown";
                await NotifyStatusUpdateAsync(this, new()
                {
                    Status = string.Format(StatusDownloadingUpdate, 0, newVersion)
                });

                var progress = new Progress<int>(percent =>
                {
                    UpdateProgress = percent;
                    Status = string.Format(StatusDownloadingUpdate, percent, newVersion);
                });

                await _appUpdateManager.DownloadAndApplyUpdatesAsync(progress);
                // 앱이 재시작되므로 이후 코드는 실행되지 않음
            }
        }
        catch (Exception)
        {
            // 업데이트 실패는 무시하고 앱 실행 계속
            IsUpdating = false;
            ShowUpdateProgress = false;
        }
    }

    [ObservableProperty]
    private string _appVersion = Helpers.GetAppVersion();

    [ObservableProperty]
    private string _status = UIStringResources.Status_PleaseWait;

    [ObservableProperty]
    private bool _appStartupSucceed = false;

    [ObservableProperty]
    private IList<string> _passedArguments = Array.Empty<string>();

    [ObservableProperty]
    private CommandLineArgumentModel? _parsedArgument;

    [ObservableProperty]
    private IList<string> _warnings = new List<string>();

    [ObservableProperty]
    private bool _isUpdating = false;

    [ObservableProperty]
    private int _updateProgress = 0;

    [ObservableProperty]
    private bool _showUpdateProgress = false;

    private readonly IApplicationService _applicationService = default!;
    private readonly IAppStartup _appStartup = default!;
    private readonly IAppMessageBox _appMessageBox = default!;
    private readonly IPreferencesManager _preferencesManager = default!;
    private readonly IResourceCacheManager _resourceCacheManager = default!;
    private readonly ICommandLineArguments _commandLineArguments = default!;
    private readonly IAppUpdateManager _appUpdateManager = default!;
}
