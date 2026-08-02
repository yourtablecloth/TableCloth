using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using Avalonia.Controls;
using Avalonia.Threading;
using System.Collections.Generic;
using System.Linq;
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

            // 사이트 Id 판정은 매처가 하나로 끝낸다(동점이면 카탈로그 순서). 여기서는 결과만 받는다 —
            // URL 형식과 사이트 Id 형식의 차이는 "열 주소가 카탈로그 대표 URL 이냐 지정된 URL 이냐"뿐이다.
            if (match.IsAccepted)
            {
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
    /// 실행 중인 인스턴스가 딥링크 페이로드를 받았을 때의 처리. 파이프 수신 스레드에서 호출되므로
    /// UI 디스패처로 옮긴 뒤 창을 활성화하고 대상 사이트로 이동한다.
    /// </summary>
    private void HandleDeepLinkPayload(string payload)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (!TableClothUri.TryParse(payload, out var request))
                return;

            ActivateMainWindow();

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

    private void ActivateMainWindow()
    {
        var window = _applicationService.GetMainWindow();

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
