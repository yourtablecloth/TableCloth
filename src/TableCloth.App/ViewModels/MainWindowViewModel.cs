using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Windows;
using TableCloth.Components;
using TableCloth.Models;
using TableCloth.Resources;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public partial class MainWindowViewModelForDesigner : MainWindowViewModel { }

public partial class MainWindowViewModel : ObservableObject
{
    protected MainWindowViewModel() { }

    [ActivatorUtilitiesConstructor]
    public MainWindowViewModel(
        IApplicationService applicationService,
        INavigationService navigationService,
        ICommandLineArguments commandLineArguments,
        ISandboxCleanupManager sandboxCleanupManager,
        IAppRestartManager appRestartManager,
        IDeepLinkActivationChannel deepLinkActivationChannel,
        IDeepLinkResolver deepLinkResolver)
    {
        _applicationService = applicationService;
        _navigationService = navigationService;
        _commandLineArguments = commandLineArguments;
        _sandboxCleanupManager = sandboxCleanupManager;
        _appRestartManager = appRestartManager;
        _deepLinkActivationChannel = deepLinkActivationChannel;
        _deepLinkResolver = deepLinkResolver;
    }

    [RelayCommand]
    private void MainWindowLoaded()
    {
        _applicationService.ApplyCosmeticChangeToMainWindow();

        // 앱이 떠 있는 동안 브라우저가 tablecloth: 링크를 실행하면 두 번째 인스턴스가 페이로드만
        // 넘기고 끝난다. 그 페이로드를 여기서 받아 처리한다.
        _deepLinkActivationChannel.StartListening(HandleDeepLinkPayload);

        // 딥링크로 시작된 경우 스플래시가 이미 그 자리에서 샌드박스를 띄웠다. 이 창은 사용자가
        // '식탁보 열기'를 골라 열린 것이므로, 같은 딥링크를 두 번 실행하지 않도록 평소 화면으로 간다.
        var parsedArg = _commandLineArguments.IsStartupTargetHandled
            ? null
            : _commandLineArguments.GetCurrent();

        if (TryNavigateToCommandLineTarget(parsedArg))
            return;

        _navigationService.NavigateToQuickStart();
    }

    /// <summary>
    /// 명령줄(딥링크 포함)이 지정한 사이트로 진입을 시도한다.
    /// </summary>
    /// <remarks>
    /// 사이트 Id 경로는 <c>--select &lt;SiteId&gt;</c> 시절부터의 하위 호환이고, 대상 URL 경로는
    /// <c>tablecloth:https://…</c> 딥링크가 쓴다. 후자는 카탈로그 도메인 게이트를 통과한 경우에만
    /// 살아남으며, 통과하면 그 URL 이 샌드박스 안에서 열릴 주소가 된다.
    /// </remarks>
    private bool TryNavigateToCommandLineTarget(CommandLineArgumentModel? parsedArg)
    {
        if (parsedArg == null)
            return false;

        // 대상 판정은 스플래시(최초 실행)와 이 창(실행 중 인스턴스)이 같은 구현을 쓴다.
        var resolution = _deepLinkResolver.Resolve(parsedArg);

        if (!resolution.IsResolved)
            return false;

        // 딥링크 진입: 링크 한 번이 곧 "샌드박스를 띄워라"라는 지시이므로 중간 화면 없이 바로 실행한다.
        // QuickStart 의 실행 경로를 그대로 타야 Data 마운트·NPKI 공유·환경 설정 옵션이 평소와 동일하게
        // 적용된다(상세 화면 경로는 이것들이 빠진다).
        if (resolution.LaunchImmediately)
            return _navigationService.NavigateToQuickStartAndLaunch(resolution.Services, resolution.AcceptedTargetUrl);

        // 예전부터 있던 `TableCloth.exe <SiteId>`(바탕화면 `.tclnk` 바로가기 등)는 종전대로 상세 화면.
        // 게이트를 통과한 URL 만 실어 보낸다 — 통과하지 못했으면 null 로 지워 검증되지 않은 주소가
        // 샌드박스 구성까지 흘러가지 않게 한다.
        var effectiveArg = parsedArg.WithResolvedTarget(resolution.ServiceIds, resolution.AcceptedTargetUrl!);

        _navigationService.NavigateToDetail(string.Empty, resolution.Services[0], effectiveArg);
        return true;
    }

    /// <summary>
    /// 실행 중인 인스턴스가 딥링크 페이로드를 받았을 때의 처리. 파이프 수신 스레드에서 호출되므로
    /// UI 디스패처로 옮긴 뒤 창을 활성화하고 대상 사이트로 이동한다.
    /// </summary>
    private void HandleDeepLinkPayload(string payload)
    {
        var application = Application.Current;

        if (application == null)
            return;

        application.Dispatcher.Invoke(() =>
        {
            if (!TableClothUri.TryParse(payload, out var request))
                return;

            ActivateMainWindow(application);

            var arguments = request.ToCanonicalArguments();

            if (arguments.Length < 1)
                return;

            // 정규 인자를 그대로 다시 해석해 최초 실행 경로와 동일하게 처리한다.
            var siteIds = new List<string>();
            var targetUrl = default(string);
            var launchImmediately = false;

            for (var i = 0; i < arguments.Length; i++)
            {
                if (string.Equals(arguments[i], ConstantStrings.TableCloth_Switch_TargetUrl, StringComparison.OrdinalIgnoreCase))
                {
                    targetUrl = i + 1 < arguments.Length ? arguments[i + 1] : null;
                    i++;
                    continue;
                }

                if (string.Equals(arguments[i], ConstantStrings.TableCloth_Switch_Launch, StringComparison.OrdinalIgnoreCase))
                {
                    launchImmediately = true;
                    continue;
                }

                siteIds.Add(arguments[i]);
            }

            TryNavigateToCommandLineTarget(new CommandLineArgumentModel(
                rawArguments: arguments,
                selectedServices: siteIds.ToArray(),
                targetUrl: targetUrl,
                launchImmediately: launchImmediately));
        });
    }

    private static void ActivateMainWindow(Application application)
    {
        var window = application.MainWindow;

        if (window == null)
            return;

        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        window.Show();
        window.Activate();
    }

    [RelayCommand]
    private void MainWindowClosed()
    {
        _sandboxCleanupManager.TryCleanup();

        if (_appRestartManager.IsRestartReserved())
            _appRestartManager.RestartNow();
    }

    private readonly IApplicationService _applicationService = default!;
    private readonly INavigationService _navigationService = default!;
    private readonly ICommandLineArguments _commandLineArguments = default!;
    private readonly ISandboxCleanupManager _sandboxCleanupManager = default!;
    private readonly IAppRestartManager _appRestartManager = default!;
    private readonly IDeepLinkActivationChannel _deepLinkActivationChannel = default!;
    private readonly IDeepLinkResolver _deepLinkResolver = default!;
}
