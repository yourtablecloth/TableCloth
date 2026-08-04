using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
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

public partial class SplashScreenViewModel : ObservableObject
{
    protected SplashScreenViewModel() { }

    [ActivatorUtilitiesConstructor]
    public SplashScreenViewModel(
        IApplicationService applicationService,
        IAppStartup appStartup,
        IAppMessageBox appMessageBox,
        IPreferencesManager preferencesManager,
        IResourceCacheManager resourceCacheManager,
        ICommandLineArguments commandLineArguments,
        IAppUpdateManager appUpdateManager,
        IAppUserInterface appUserInterface,
        IDeepLinkResolver deepLinkResolver,
        TaskFactory taskFactory)
    {
        _applicationService = applicationService;
        _appStartup = appStartup;
        _appMessageBox = appMessageBox;
        _preferencesManager = preferencesManager;
        _resourceCacheManager = resourceCacheManager;
        _commandLineArguments = commandLineArguments;
        _appUpdateManager = appUpdateManager;
        _appUserInterface = appUserInterface;
        _deepLinkResolver = deepLinkResolver;
        _taskFactory = taskFactory;
    }

    public event EventHandler<StatusUpdateRequestEventArgs>? StatusUpdate;
    public event EventHandler<DialogRequestEventArgs>? InitializeDone;

    /// <summary>
    /// 딥링크 결과 화면에서 사용자가 '식탁보 열기'를 골랐을 때 발생. 스플래시를 닫고 메인 창을
    /// 여는 것은 App 라이프사이클의 책임이라 여기서는 알리기만 한다.
    /// </summary>
    public event EventHandler? OpenMainWindowRequested;

    public async Task NotifyStatusUpdateAsync(object sender, StatusUpdateRequestEventArgs e, CancellationToken cancellationToken = default)
        => await _taskFactory.StartNew(() => StatusUpdate?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    public async Task NotifyInitializedAsync(object sender, DialogRequestEventArgs e, CancellationToken cancellationToken = default)
        => await _taskFactory.StartNew(() => InitializeDone?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

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
                    // Release Health: 세션(DAU/MAU)·버전 채택/리텐션·crash-free 비율 측정.
                    // 이 Init 은 진단 동의(UseLogCollection)에 게이트되어 있어 프라이버시 동의를 그대로 상속한다.
                    o.AutoSessionTracking = true;
                    o.Release = $"TableCloth@{Helpers.GetAppVersion()}";
                    o.Environment = "host"; // 샌드박스 세션(Spork)과 구분
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

    /// <summary>
    /// 이번 실행이 "딥링크로 시작된 것"인지 판정한다. 부팅이 끝나 카탈로그가 로드된 뒤에만 의미가 있다.
    /// </summary>
    /// <remarks>
    /// 인식에 실패하면 <see cref="DeepLinkResolution.ShouldLaunchImmediately"/> 가 거짓이 되고,
    /// App 은 스플래시를 닫고 평소대로 메인 창을 여는 경로로 간다(별도 안내 없음).
    /// </remarks>
    public DeepLinkResolution ResolveStartupDeepLink()
        => _deepLinkResolver.Resolve(ParsedArgument);

    /// <summary>
    /// 딥링크 대상 샌드박스를 실행하고, 그 결과를 스플래시의 액션 화면으로 바꾼다.
    /// </summary>
    public async Task RunDeepLinkLaunchAsync(DeepLinkResolution resolution, CancellationToken cancellationToken = default)
    {
        _deepLinkResolution = resolution;
        DeepLinkTargetName = DescribeTarget(resolution);

        IsDeepLinkMode = true;
        ShowDeepLinkActions = false;
        ShowUpdateProgress = false;

        Status = string.Format(UIStringResources.Splash_DeepLink_Launching, DeepLinkTargetName);

        var succeed = false;

        try
        {
            // QuickStart 뷰모델의 실행 경로를 그대로 탄다 — Data 마운트·NPKI 공유·환경 설정이
            // 평소 실행과 완전히 동일하게 적용되도록 구성 로직을 복제하지 않는다.
            var quickStart = _appUserInterface.CreateQuickStartPageViewModel();
            succeed = await quickStart.LaunchForDeepLinkAsync(
                resolution.Services, resolution.AcceptedTargetUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            // 실행 실패가 앱을 죽이면 사용자는 '다시 시도'조차 못 한다. 사유만 보여주고 액션 화면으로.
            _appMessageBox.DisplayError(ex, false);
        }

        // 최초 실행 인자는 여기서 소비된 것으로 표시한다. 이후 사용자가 '식탁보 열기'를 골라
        // 메인 창이 열려도 같은 딥링크를 두 번 실행하지 않는다.
        _commandLineArguments.IsStartupTargetHandled = true;

        DeepLinkLaunchSucceeded = succeed;
        // 한국어 리소스는 '{0}{1}' 형태로 목적격 조사 자리를 둔다(영어 리소스는 {1} 을 쓰지 않으며,
        // string.Format 은 남는 인자를 무시한다). 받침 판정이 안 되면 '을(를)' 병기로 떨어진다.
        DeepLinkResultMessage = string.Format(
            succeed ? UIStringResources.Splash_DeepLink_Succeeded : UIStringResources.Splash_DeepLink_Failed,
            DeepLinkTargetName,
            KoreanParticle.EulReul(DeepLinkTargetName));
        Status = succeed ? UIStringResources.Status_Done : UIStringResources.Splash_DeepLink_FailedStatus;
        ShowDeepLinkActions = true;
    }

    /// <summary>딥링크 결과 화면에 표시할 대상 이름. 사이트가 여럿이면 쉼표로 잇는다.</summary>
    private static string DescribeTarget(DeepLinkResolution resolution)
        => string.Join(", ", resolution.Services.Select(x => x.DisplayName));

    /// <summary>샌드박스는 이미 독립 프로세스로 떠 있으므로, 식탁보만 종료한다.</summary>
    [RelayCommand]
    private void CloseAfterDeepLink()
        => _applicationService.Shutdown(CodeResources.ExitCode_Succeed);

    [RelayCommand]
    private async Task RetryDeepLinkLaunch()
    {
        if (_deepLinkResolution == null)
            return;

        await RunDeepLinkLaunchAsync(_deepLinkResolution);
    }

    [RelayCommand]
    private void OpenTableCloth()
        => OpenMainWindowRequested?.Invoke(this, EventArgs.Empty);

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
    [NotifyPropertyChangedFor(nameof(ShowBusyIndicator))]
    private bool _showUpdateProgress = false;

    /// <summary>딥링크로 시작되어 스플래시가 종착 화면 역할을 하는 중인지.</summary>
    [ObservableProperty]
    private bool _isDeepLinkMode = false;

    /// <summary>실행이 끝나 액션(닫기/다시 시도/식탁보 열기)을 고를 수 있는 상태인지.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowBusyIndicator))]
    [NotifyPropertyChangedFor(nameof(SplashWindowHeight))]
    private bool _showDeepLinkActions = false;

    /// <summary>
    /// 무한 회전 표시자를 보여줄지. 부팅이 끝나 딥링크 액션을 고르는 중이거나 업데이트 진행바가
    /// 떠 있으면 감춘다 — 할 일이 끝났는데도 계속 도는 표시자는 아직 작업 중인 것처럼 보인다.
    /// </summary>
    public bool ShowBusyIndicator
        => !ShowUpdateProgress && !ShowDeepLinkActions;

    /// <summary>
    /// 스플래시 창 높이. 딥링크 종착 화면에서는 결과 문구와 버튼 줄이 더해져 기본 높이로는 버튼이
    /// 잘리므로 창을 키운다(옵션 창에서 겪은 것과 같은 고정 높이 클리핑).
    /// </summary>
    /// <remarks>
    /// 뷰 메트릭이지만 여기 두는 이유: 이 창은 Style="{DynamicResource MainWindowStyle}" 가 이미
    /// 지정돼 있어 Window.Style 에 DataTrigger 를 얹을 수 없다(Style 중복 설정 오류).
    /// </remarks>
    public double SplashWindowHeight
        => ShowDeepLinkActions ? 520d : 400d;

    [ObservableProperty]
    private bool _deepLinkLaunchSucceeded = false;

    [ObservableProperty]
    private string _deepLinkTargetName = string.Empty;

    [ObservableProperty]
    private string _deepLinkResultMessage = string.Empty;

    private DeepLinkResolution? _deepLinkResolution;

    private readonly IApplicationService _applicationService = default!;
    private readonly IAppStartup _appStartup = default!;
    private readonly IAppMessageBox _appMessageBox = default!;
    private readonly IPreferencesManager _preferencesManager = default!;
    private readonly IResourceCacheManager _resourceCacheManager = default!;
    private readonly ICommandLineArguments _commandLineArguments = default!;
    private readonly IAppUpdateManager _appUpdateManager = default!;
    private readonly IAppUserInterface _appUserInterface = default!;
    private readonly IDeepLinkResolver _deepLinkResolver = default!;
    private readonly TaskFactory _taskFactory = default!;
}
