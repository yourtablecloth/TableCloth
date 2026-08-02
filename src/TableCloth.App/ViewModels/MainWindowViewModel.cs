using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TableCloth.Components;
using TableCloth.Models;
using TableCloth.Models.Catalog;
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
        IResourceCacheManager resourceCacheManager,
        INavigationService navigationService,
        ICommandLineArguments commandLineArguments,
        ISandboxCleanupManager sandboxCleanupManager,
        IAppRestartManager appRestartManager,
        IDeepLinkActivationChannel deepLinkActivationChannel)
    {
        _applicationService = applicationService;
        _resourceCacheManager = resourceCacheManager;
        _navigationService = navigationService;
        _commandLineArguments = commandLineArguments;
        _sandboxCleanupManager = sandboxCleanupManager;
        _appRestartManager = appRestartManager;
        _deepLinkActivationChannel = deepLinkActivationChannel;
    }

    [RelayCommand]
    private void MainWindowLoaded()
    {
        _applicationService.ApplyCosmeticChangeToMainWindow();

        // 앱이 떠 있는 동안 브라우저가 tablecloth: 링크를 실행하면 두 번째 인스턴스가 페이로드만
        // 넘기고 끝난다. 그 페이로드를 여기서 받아 처리한다.
        _deepLinkActivationChannel.StartListening(HandleDeepLinkPayload);

        var parsedArg = _commandLineArguments.GetCurrent();

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

        var catalog = _resourceCacheManager.CatalogDocument;
        var services = catalog.Services;

        // 딥링크는 `tablecloth:wooribank` 처럼 카탈로그와 대소문자가 다를 수 있다. 여기서 카탈로그의
        // 정식 Id 로 정규화해, 이후 단계(상세 페이지의 재조회, 샌드박스 구성, 게스트의 Spork)가 모두
        // 같은 값을 보게 한다. 하위 단계들은 Ordinal 비교라 정규화하지 않으면 조용히 빈 선택이 된다.
        var requestedServiceIds = parsedArg.SelectedServices?.ToArray() ?? Array.Empty<string>();
        var selectedServiceIds = services
            .Where(x => requestedServiceIds.Contains(x.Id, StringComparer.OrdinalIgnoreCase))
            .Select(x => x.Id)
            .ToArray();
        var acceptedTargetUrl = default(string);

        if (!string.IsNullOrWhiteSpace(parsedArg.TargetUrl))
        {
            var match = CatalogTargetUrlMatcher.Match(catalog, parsedArg.TargetUrl, selectedServiceIds);

            if (match.IsAccepted)
            {
                selectedServiceIds = match.ServiceIds.ToArray();
                acceptedTargetUrl = match.AcceptedUrl;
            }
            else if (parsedArg.LaunchImmediately &&
                     match.Reason == CatalogTargetUrlRejectionReason.AmbiguousCandidates &&
                     match.ServiceIds.Count > 0 &&
                     match.ServiceIds.Count <= MaxAmbiguousServicesToInstall)
            {
                // 같은 등록 도메인에 서비스가 여럿이라 하나로 확정할 수 없는 경우
                // (예: spib.wooribank.com → 우리은행 개인/기업 동점).
                // 딥링크는 화면을 띄우지 않는 것이 계약이므로 후보 전체의 보안 프로그램을 설치하고
                // 요청받은 URL 을 연다. 같은 은행의 서비스라 패키지 구성이 거의 겹치고, 사용자에게
                // 선택을 되묻는 것보다 이쪽이 링크의 의도에 맞다.
                selectedServiceIds = match.ServiceIds.ToArray();
                acceptedTargetUrl = match.AcceptedUrl;
            }
        }

        var selectedServices = services.Where(x => selectedServiceIds.Contains(x.Id)).ToArray();

        if (selectedServices.Length < 1)
            return false;

        // 딥링크 진입: 링크 한 번이 곧 "샌드박스를 띄워라"라는 지시이므로 중간 화면 없이 바로 실행한다.
        // QuickStart 의 실행 경로를 그대로 타야 Data 마운트·NPKI 공유·환경 설정 옵션이 평소와 동일하게
        // 적용된다(상세 화면 경로는 이것들이 빠진다).
        if (parsedArg.LaunchImmediately)
            return _navigationService.NavigateToQuickStartAndLaunch(selectedServices, acceptedTargetUrl);

        // 예전부터 있던 `TableCloth.exe <SiteId>`(바탕화면 `.tclnk` 바로가기 등)는 종전대로 상세 화면.
        // 게이트를 통과한 URL 만 실어 보낸다 — 통과하지 못했으면 null 로 지워 검증되지 않은 주소가
        // 샌드박스 구성까지 흘러가지 않게 한다.
        var effectiveArg = parsedArg.WithResolvedTarget(selectedServiceIds, acceptedTargetUrl);

        _navigationService.NavigateToDetail(string.Empty, selectedServices[0], effectiveArg);
        return true;
    }

    /// <summary>
    /// 동점 후보를 한꺼번에 설치해도 되는 최대 개수.
    /// </summary>
    /// <remarks>
    /// 실제로 동점이 나는 경우는 같은 은행의 개인/기업(우리), 개인/기업/저축(하나) 정도라 2~3개다.
    /// 반면 <c>fsb.or.kr</c> 처럼 공용 호스팅 도메인은 25개까지 동점이 날 수 있는데, 그것들은 서로
    /// 다른 저축은행이라 전부 설치하는 것이 의미도 없고 시간만 오래 걸린다. 그 경우엔 URL 을 버리고
    /// 평소처럼 앱만 연다.
    /// </remarks>
    private const int MaxAmbiguousServicesToInstall = 3;

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
    private readonly IResourceCacheManager _resourceCacheManager = default!;
    private readonly INavigationService _navigationService = default!;
    private readonly ICommandLineArguments _commandLineArguments = default!;
    private readonly ISandboxCleanupManager _sandboxCleanupManager = default!;
    private readonly IAppRestartManager _appRestartManager = default!;
    private readonly IDeepLinkActivationChannel _deepLinkActivationChannel = default!;
}
