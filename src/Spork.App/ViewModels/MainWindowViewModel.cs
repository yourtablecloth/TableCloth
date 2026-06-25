using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Spork.Browsers;
using Spork.Components;
using Spork.Steps;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using TableCloth;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Models.UserData;
using TableCloth.Resources;

namespace Spork.ViewModels
{
    public partial class MainWindowViewModelForDesigner : MainWindowViewModel
    {
        public IList<StepItemViewModelForDesigner> InstallStepsForDesigner
            => DesignTimeResources.DesignTimePackageInformations.Select((x, i) =>
            {
                var triState = DesignTimeResources.ConvertToTriState(i);

                var model = new StepItemViewModelForDesigner
                {
                    Argument = new InstallItemViewModel(),
                    TargetSiteName = "Sample Site",
                    TargetSiteUrl = "https://www.example.com/",
                    PackageName = x.Name,
                    Installed = triState,
                    StatusMessage = "Status",
                    ErrorMessage = DesignTimeResources.GenerateRandomErrorMessage(i),
                    ProgressRate = triState.HasValue ? 100d : 50d,
                    ShowProgress = !triState.HasValue,
                };

                return model;

            }).ToList();

        public IList<CatalogInternetService> CatalogServicesForDesigner
            => DesignTimeResources.DesignTimeCatalogDocument.Services;
    }

    public partial class MainWindowViewModel : ObservableObject
    {
        protected MainWindowViewModel() { }

        [ActivatorUtilitiesConstructor]
        public MainWindowViewModel(
            IResourceCacheManager resourceCacheManager,
            IAppUserInterface appUserInterface,
            IVisualThemeManager visualThemeManager,
            ICommandLineArguments commandLineArguments,
            IStepsComposer stepsComposer,
            IStepsPlayer stepsPlayer,
            IAppMessageBox appMessageBox,
            IUserDataStore userDataStore,
            IInstallRecordStore installRecordStore,
            IShortcutCreator shortcutCreator,
            IWebBrowserServiceFactory webBrowserServiceFactory,
            IX509CertScanner certScanner,
            TaskFactory taskFactory)
        {
            // Application은 DI로 받지 않고 WPF 표준 정적 참조 사용 (Spork.App ApplicationService와 동일 사유).
            _resourceCacheManager = resourceCacheManager;
            _appUserInterface = appUserInterface;
            _visualThemeManager = visualThemeManager;
            _commandLineArguments = commandLineArguments;
            _stepsComposer = stepsComposer;
            _stepsPlayer = stepsPlayer;
            _appMessageBox = appMessageBox;
            _userDataStore = userDataStore;
            _installRecordStore = installRecordStore;
            _shortcutCreator = shortcutCreator;
            _webBrowserServiceFactory = webBrowserServiceFactory;
            _certScanner = certScanner;
            _taskFactory = taskFactory;
        }

        private static Application _application
            => Application.Current
               ?? throw new InvalidOperationException("Application.Current is not yet available.");
        private readonly IResourceCacheManager _resourceCacheManager;
        private readonly IAppUserInterface _appUserInterface;
        private readonly IVisualThemeManager _visualThemeManager;
        private readonly ICommandLineArguments _commandLineArguments;
        private readonly IStepsComposer _stepsComposer;
        private readonly IStepsPlayer _stepsPlayer;
        private readonly IAppMessageBox _appMessageBox;
        private readonly IUserDataStore _userDataStore;
        private readonly IInstallRecordStore _installRecordStore;
        private readonly IShortcutCreator _shortcutCreator;
        private readonly IWebBrowserServiceFactory _webBrowserServiceFactory;
        private readonly IX509CertScanner _certScanner;
        private readonly TaskFactory _taskFactory;

        /// <summary>
        /// 사용자가 카탈로그 UI를 통해 진입했는지(true) 또는 명령줄 --select로 진입했는지(false).
        /// 카탈로그 진입의 경우 설치 완료 후 "카탈로그로 돌아가기" UX를 제공한다.
        /// </summary>
        private bool _enteredViaCatalog;

        // 사용자 데이터는 IUserDataStore.Current 가 단일 진실. _userData 는 그 단축 참조.
        // 디바운스 저장도 IUserDataStore.ScheduleSave 가 담당한다.
        private SporkUserData _userData => _userDataStore.Current;
        private bool _suppressUserDataSave;

        private static readonly PropertyGroupDescription CatalogGroupDescription =
            new PropertyGroupDescription(nameof(CatalogInternetService.CategoryDisplayName));

        [RelayCommand]
        private void ShowErrorMessage(string errorMessage)
        {
            _appMessageBox.DisplayError(errorMessage, true);
        }

        [RelayCommand]
        private async Task MainWindowLoaded()
        {
            _visualThemeManager.ApplyAutoThemeChange(_application.MainWindow);

            var parsedArgs = _commandLineArguments.GetCurrent();
            ShowDryRunNotification = parsedArgs.DryRun;

            await NotifyWindowLoadedAsync(this, EventArgs.Empty);

            await _userDataStore.EnsureLoadedAsync();
            await _installRecordStore.EnsureLoadedAsync();
            _suppressUserDataSave = true;
            try
            {
                ShowFavoritesOnly = _userData.ShowFavoritesOnly;
            }
            finally
            {
                _suppressUserDataSave = false;
            }

            // 사용자가 Spork를 닫은 뒤에도 샌드박스 안에서 다시 띄울 수 있도록 데스크톱에 바로가기를 만든다.
            // 매 실행마다 호출되어도 기존 .lnk를 덮어쓰므로 안전.
            await TryCreateSporkShortcutAsync();

            if (parsedArgs.SelectedServices.Any())
            {
                // 명령줄로 사이트가 지정되어 들어온 경우: 종전대로 즉시 설치 흐름.
                // 설치 성공 시 자동 종료 (--select 기반 바로가기/외부 호출 호환).
                _enteredViaCatalog = false;
                await RecordUsageAsync(parsedArgs.SelectedServices);
                await EnterStepsModeAsync(_stepsComposer.ComposeSteps(), showPrecautions: true);
            }
            else
            {
                // 명령줄에 사이트가 없으면: 카탈로그 UI를 보여주고 사용자가 선택하도록 함.
                _enteredViaCatalog = true;
                LoadCatalogForBrowsing();
                ShowCatalogView = true;
            }
        }

        private async Task TryCreateSporkShortcutAsync()
        {
            try
            {
                // 단일 파일 게시에서 Assembly.Location은 빈 문자열을 반환하므로 Environment.ProcessPath 사용
                var sporkExePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(sporkExePath) || !File.Exists(sporkExePath))
                    return;

                // TableCloth.exe 가 entry 일 때는 'spork' verb 가 인자에 반드시 들어가야 한다.
                // 인자 없이 TableCloth.exe 를 띄우면 호스트 런처 모드로 들어가버려 sandbox 안에서는
                // 의미 있는 동작을 못한다. BrandStrings.ShortcutArguments 가 entry 종류에 따라 알맞은
                // 인자를 돌려준다(TableCloth → "spork", Spork 단독 → "").
                await _shortcutCreator.CreateShortcutOnDesktopAsync(
                    destinationPath: sporkExePath,
                    linkName: BrandStrings.ShortcutLinkName,
                    arguments: BrandStrings.ShortcutArguments,
                    iconFilePath: sporkExePath,
                    description: BrandStrings.ShortcutDescription).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 바로가기 생성 실패가 카탈로그/설치 흐름을 막아서는 안 된다.
                _appMessageBox.DisplayError(ex, false);
            }
        }

        private void LoadCatalogForBrowsing()
        {
            var catalog = _resourceCacheManager.CatalogDocument;

            var ordered = catalog.Services.OrderBy(service =>
            {
                var fieldInfo = typeof(CatalogInternetServiceCategory).GetField(service.Category.ToString());

                if (fieldInfo == null)
                    return default;

                var attr = fieldInfo.GetCustomAttribute<EnumDisplayOrderAttribute>();

                if (attr == null)
                    return default;

                return attr.Order;
            }).ToList();

            // 저장된 즐겨찾기를 카탈로그 항목에 반영
            var favSet = new HashSet<string>(_userData.Favorites ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (var service in ordered)
                service.IsFavorite = favSet.Contains(service.Id);

            // 카탈로그가 생성하는 모든 fingerprint 를 수집해 영속 저장소에서 더 이상 유효하지 않은 항목을 청소.
            // 무한 누적 방지 + 카탈로그에서 사이트/패키지가 제거되면 자동으로 기록도 삭제됨.
            _installRecordStore.PruneStaleFingerprints(CollectActiveCatalogFingerprints(ordered));

            // 각 사이트의 설치 완료 여부를 계산해 카탈로그 카드의 배지 표시 상태를 결정.
            RefreshIsAllInstalledFlags(ordered);

            CatalogServices = ordered;

            var view = (CollectionView)CollectionViewSource.GetDefaultView(CatalogServices);

            if (view != null)
            {
                view.Filter = item => CatalogInternetService.IsMatchedItem(item, SearchKeyword, ShowFavoritesOnly);

                if (!view.GroupDescriptions.Contains(CatalogGroupDescription))
                    view.GroupDescriptions.Add(CatalogGroupDescription);
            }

            // 보조 프로그램 목록도 동일 카탈로그 문서에서 가져온다 (XML의 <Companions> 요소).
            // 아이콘이 없어 단순 텍스트 리스트로 노출되며 카테고리 그룹화는 없다.
            CatalogCompanions = (catalog.Companions ?? new List<CatalogCompanion>())
                .Where(c => !string.IsNullOrWhiteSpace(c?.DisplayName))
                .OrderBy(c => c.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            // 현재 환경의 NPKI 위치들(WSB canonical / 실제 LocalLow / Desktop\NPKI 마운트 / USB)을
            // 스캔하여 사용 가능한 인증서를 카탈로그 탭에서 보여준다. 모드 1(식탁보+WSB)은 물론
            // 사용자가 직접 만든 VM(모드 2)에서도 동작한다. 만료된 인증서도 포함되며 UI는 취소선으로 구분한다.
            try
            {
                CatalogCertificates = _certScanner.ScanLocalNpkiCertificates().ToList();
            }
            catch
            {
                // 인증서 스캔 실패는 카탈로그 자체 흐름을 막지 않는다.
                CatalogCertificates = new List<X509CertPair>();
            }

            PropertyChanged -= ViewModel_PropertyChanged;
            PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(nameof(SearchKeyword), e.PropertyName, StringComparison.Ordinal) ||
                string.Equals(nameof(ShowFavoritesOnly), e.PropertyName, StringComparison.Ordinal))
            {
                var view = CollectionViewSource.GetDefaultView(CatalogServices);
                view?.Refresh();
            }

            if (string.Equals(nameof(ShowFavoritesOnly), e.PropertyName, StringComparison.Ordinal) && !_suppressUserDataSave)
            {
                _userData.ShowFavoritesOnly = ShowFavoritesOnly;
                _userDataStore.ScheduleSave();
            }
        }

        [RelayCommand]
        private void ToggleFavorite(CatalogInternetService service)
        {
            if (service == null || string.IsNullOrWhiteSpace(service.Id))
                return;

            _userData.Favorites ??= new List<string>();

            if (service.IsFavorite)
            {
                if (!_userData.Favorites.Contains(service.Id, StringComparer.OrdinalIgnoreCase))
                    _userData.Favorites.Add(service.Id);
            }
            else
            {
                _userData.Favorites.RemoveAll(x => string.Equals(x, service.Id, StringComparison.OrdinalIgnoreCase));
            }

            // ShowFavoritesOnly가 켜져 있을 때만 필터 결과가 바뀌므로 그때만 refresh. 그렇지 않으면
            // 별 아이콘은 IsFavorite 바인딩으로 즉시 갱신되고 카탈로그 가시성은 변하지 않는다.
            // (refresh는 266개 항목 필터 재평가 + 그룹 재구성을 UI 스레드에서 수행하므로 회피 가치가 크다.)
            if (ShowFavoritesOnly)
            {
                var view = CollectionViewSource.GetDefaultView(CatalogServices);
                view?.Refresh();
            }

            // 디스크 쓰기는 디바운스 + fire-and-forget. 빠른 클릭 시 마지막 상태 1회만 저장.
            _userDataStore.ScheduleSave();
        }

        // 디바운스 저장 로직은 IUserDataStore.ScheduleSave 로 이전됨. UI 측은 변형 후 호출만 하면 된다.

        /// <summary>
        /// 현재 카탈로그가 만들어내는 모든 install fingerprint 를 수집한다. lazy prune 입력으로 사용.
        /// </summary>
        private static IEnumerable<string> CollectActiveCatalogFingerprints(IEnumerable<CatalogInternetService> services)
        {
            foreach (var service in services ?? Enumerable.Empty<CatalogInternetService>())
                foreach (var fp in CollectFingerprintsForService(service))
                    yield return fp;
        }

        /// <summary>
        /// 한 사이트가 카탈로그 상 정의하는 모든 fingerprint(패키지/Edge 확장/CustomBootstrap). 본 사이트의
        /// 설치 완료 여부 판정과 prune 입력 양쪽에서 같은 규칙을 공유한다.
        /// </summary>
        private static IEnumerable<string> CollectFingerprintsForService(CatalogInternetService service)
        {
            if (service == null)
                yield break;

            foreach (var package in service.Packages ?? Enumerable.Empty<CatalogPackageInformation>())
                yield return PackageFingerprints.ForPackage(package.Url, package.Arguments);

            foreach (var extension in service.EdgeExtensions ?? Enumerable.Empty<CatalogEdgeExtensionInformation>())
                yield return PackageFingerprints.ForEdgeExtension(extension.ExtensionId);

            if (!string.IsNullOrWhiteSpace(service.CustomBootstrap))
                yield return PackageFingerprints.ForPowerShellScript(service.CustomBootstrap);
        }

        /// <summary>
        /// 각 사이트의 IsAllInstalled 플래그를 영속 저장소의 fingerprint 집합과 비교해 갱신한다.
        /// 정의된 fingerprint 가 0개인 사이트(설치할 게 없는 카탈로그 항목)는 trivially 설치 완료로 본다.
        /// </summary>
        private void RefreshIsAllInstalledFlags(IEnumerable<CatalogInternetService> services)
        {
            foreach (var service in services ?? Enumerable.Empty<CatalogInternetService>())
            {
                var serviceFingerprints = CollectFingerprintsForService(service).ToList();
                service.IsAllInstalled = serviceFingerprints.Count > 0
                    && serviceFingerprints.All(_installRecordStore.IsInstalled);
            }
        }

        [RelayCommand]
        private void ShowCertificateDetails(X509CertPair pair)
        {
            // Windows 표준 인증서 속성 창(crystui DisplayCertificate)을 띄운다. 만료 여부와 상관없이
            // 모든 인증서는 상세 보기가 가능해야 한다.
            if (pair == null || pair.PublicKey == null || pair.PublicKey.Length == 0)
                return;

            try
            {
                // SYSLIB0057: new X509Certificate2(byte[])는 .NET 9+에서 obsolete. X509CertificateLoader를 사용.
                using (var cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(pair.PublicKey))
                {
                    System.Security.Cryptography.X509Certificates.X509Certificate2UI.DisplayCertificate(cert);
                }
            }
            catch (Exception ex)
            {
                _appMessageBox.DisplayError(ex, false);
            }
        }

        [RelayCommand]
        private void OpenCompanionUrl(CatalogCompanion companion)
        {
            // 보조 프로그램은 저작권/EULA 동의 흐름을 사용자가 직접 거쳐야 하므로 자동 설치하지 않는다.
            // 대신 공식 다운로드 페이지를 브라우저로 열어 사용자가 직접 진행하도록 한다.
            if (companion == null || string.IsNullOrWhiteSpace(companion.Url))
                return;

            try
            {
                var browser = _webBrowserServiceFactory.GetWindowsSandboxDefaultBrowserService();
                Process.Start(browser.CreateWebPageOpenRequest(companion.Url, ProcessWindowStyle.Maximized));
            }
            catch (Exception ex)
            {
                _appMessageBox.DisplayError(ex, false);
            }
        }

        [RelayCommand]
        private async Task CatalogItemActivate()
        {
            if (SelectedCatalogService == null)
                return;

            await EnterCatalogInstallFlowAsync(SelectedCatalogService, forceReinstall: false);
        }

        /// <summary>
        /// 카탈로그 카드의 녹색 체크 배지 위로 마우스를 올리면 새로 고침 아이콘으로 morph 되고,
        /// 클릭 시 본 명령이 발화한다. 확인 다이얼로그로 사용자 의사를 한 번 받은 뒤 강제 재설치 흐름을
        /// 진행한다. 배지는 IsAllInstalled=true 일 때만 표시되므로 본 명령의 대상은 항상
        /// "이미 모든 패키지가 설치된 사이트" 이다.
        /// </summary>
        [RelayCommand]
        private async Task ForceReinstallSite(CatalogInternetService service)
        {
            if (service == null)
                return;

            var confirm = _appMessageBox.DisplayQuestion(
                StringResources.Spork_ForceReinstall_Confirm(service.DisplayName),
                MessageBoxButton.YesNo,
                MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes)
                return;

            await EnterCatalogInstallFlowAsync(service, forceReinstall: true);
        }

        /// <summary>
        /// 카탈로그에서 한 사이트의 설치 흐름을 진행하는 공통 코드. 카드 클릭(force=false)과
        /// 배지 클릭(force=true) 양쪽 진입점이 본 메서드를 거친다.
        /// </summary>
        private async Task EnterCatalogInstallFlowAsync(CatalogInternetService service, bool forceReinstall)
        {
            // 모달 동안 다음 사이트 선택을 위해 선택값을 마지막에 초기화한다.
            var siteId = service.Id;
            var siteUrl = service.Url;

            await RecordUsageAsync(new[] { siteId });
            var steps = _stepsComposer.ComposeStepsForSites(new[] { siteId }, forceReinstall).ToList();

            // 호환성 주의 사항은 모달 진입 전에 별도 다이얼로그로 안내한다.
            var catalog = _resourceCacheManager.CatalogDocument;
            var targets = new[] { siteId };
            if (SharedExtensions.HasAnyCompatNotes(catalog, targets))
            {
                var precautions = _appUserInterface.CreatePrecautionsWindow(targets);
                precautions.ShowDialog();
            }

            // 설치 진행을 화면 전환 없이 모달로 처리. 모달은 자체적으로 단계를 실행하고
            // 성공 시 자동 닫기, 실패 시 닫기 버튼을 노출한다.
            var installWindow = _appUserInterface.CreateInstallStepsWindow(
                steps,
                ShowDryRunNotification,
                targetTitle: service.DisplayName,
                targetIconKey: siteId);
            installWindow.ShowDialog();

            // 모달 동안 install Step 들이 fingerprint 를 기록했을 수 있으므로 배지 상태를 즉시 재계산.
            // 방금 설치 완료한 사이트는 이 호출 직후 카드에 체크 배지가 표시된다.
            RefreshIsAllInstalledFlags(CatalogServices);

            SelectedCatalogService = null;

            // 설치가 시도된 경우(성공/실패 무관)엔 사이트를 열어 사용자가 바로 진행/문제 해결할 수 있게 한다.
            // VM.Succeeded 가 null 이면 사용자가 모달을 닫아 취소한 케이스라 자동 오픈을 건너뛴다.
            var installAttempted = installWindow.ViewModel.Succeeded.HasValue;
            if (installAttempted && !string.IsNullOrWhiteSpace(siteUrl))
                TryOpenSiteUrls(new[] { siteUrl });
        }

        private Task RecordUsageAsync(IEnumerable<string> siteIds)
        {
            var now = DateTime.UtcNow;
            _userData.LastUsedAt ??= new Dictionary<string, DateTime>();

            foreach (var id in siteIds ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                _userData.LastUsedAt[id] = now;
            }

            _userDataStore.ScheduleSave();
            return Task.CompletedTask;
        }

        private async Task EnterStepsModeAsync(IEnumerable<StepItemViewModel> steps, bool showPrecautions)
        {
            InstallSteps = new ObservableCollection<StepItemViewModel>(steps);
            ShowCatalogView = false;
            ShowStepsView = true;

            var parsedArgs = _commandLineArguments.GetCurrent();
            var catalog = _resourceCacheManager.CatalogDocument;
            var targets = SelectedCatalogService != null
                ? new[] { SelectedCatalogService.Id }
                : parsedArgs.SelectedServices.ToArray();

            if (showPrecautions && SharedExtensions.HasAnyCompatNotes(catalog, targets))
            {
                var window = _appUserInterface.CreatePrecautionsWindow(targets);
                window.ShowDialog();
            }

            await MainWindowInstallPackagesAsync();
        }

        [RelayCommand]
        private async Task MainWindowInstallPackages()
            => await MainWindowInstallPackagesAsync();

        private async Task MainWindowInstallPackagesAsync()
        {
            if (InstallSteps == null || InstallSteps.Count == 0)
                return;

            // 이 경로는 명령줄(--select) 진입 전용이다. 카탈로그 진입은 모달
            // (InstallStepsWindow)에서 별도로 단계를 실행한다.
            var targetUrls = ResolveTargetSiteUrls();
            var hasAnyFailure = await _stepsPlayer.PlayStepsAsync(InstallSteps, ShowDryRunNotification);

            if (hasAnyFailure)
            {
                // 실패 시 StepsView를 유지하여 사용자가 결과를 확인할 수 있게 한다.
                return;
            }

            // 설치 성공: 대상 사이트 URL을 (가능하면 Edge로) 자동으로 연 뒤 외부 호출 측의
            // 자동 종료 기대를 유지한다.
            TryOpenSiteUrls(targetUrls);
            await RequestCloseAsync(this, EventArgs.Empty);
        }

        private IList<string> ResolveTargetSiteUrls()
        {
            var catalog = _resourceCacheManager.CatalogDocument;

            IEnumerable<string> targetIds;
            if (_enteredViaCatalog)
                targetIds = SelectedCatalogService != null ? new[] { SelectedCatalogService.Id } : Enumerable.Empty<string>();
            else
                targetIds = _commandLineArguments.GetCurrent().SelectedServices;

            var idSet = new HashSet<string>(targetIds ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            return catalog.Services
                .Where(s => idSet.Contains(s.Id))
                .Select(s => s.Url)
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .ToList();
        }

        private void TryOpenSiteUrls(IList<string> urls)
        {
            if (urls == null || urls.Count == 0)
                return;

            try
            {
                var browser = _webBrowserServiceFactory.GetWindowsSandboxDefaultBrowserService();
                foreach (var url in urls)
                    Process.Start(browser.CreateWebPageOpenRequest(url, ProcessWindowStyle.Maximized));
            }
            catch (Exception ex)
            {
                // 브라우저 실행 실패는 설치 흐름을 망치지 않도록 비치명적으로 처리한다.
                _appMessageBox.DisplayError(ex, false);
            }
        }

[RelayCommand]
        private void AboutThisApp()
        {
            var aboutWindow = _appUserInterface.CreateAboutWindow();
            aboutWindow.ShowDialog();
        }

        [RelayCommand]
        private void ReportSite()
        {
            var siteReportWindow = _appUserInterface.CreateSiteReportWindow();
            siteReportWindow.ShowDialog();
        }

        [RelayCommand]
        private void ShowDebugInfo()
        {
            _appMessageBox.DisplayInfo(StringResources.TableCloth_DebugInformation(
                Process.GetCurrentProcess().ProcessName,
                string.Join(" ", _commandLineArguments.GetCurrent().RawArguments),
                _commandLineArguments.GetCurrent().ToString())
            );
        }

        public event EventHandler WindowLoaded;
        public event EventHandler CloseRequested;

        public async Task NotifyWindowLoadedAsync(object sender, EventArgs e, CancellationToken cancellationToken = default)
            => await _taskFactory.StartNew(() => WindowLoaded?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

        public async Task RequestCloseAsync(object sender, EventArgs e, CancellationToken cancellationToken = default)
            => await _taskFactory.StartNew(() => CloseRequested?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

        [ObservableProperty]
        private bool _showDryRunNotification;

        [ObservableProperty]
        private bool _showCatalogView;

        [ObservableProperty]
        private bool _showStepsView;

        [ObservableProperty]
        private bool _showFavoritesOnly;

        [ObservableProperty]
        private IList<StepItemViewModel> _installSteps = new ObservableCollection<StepItemViewModel>();

        [ObservableProperty]
        private IList<CatalogInternetService> _catalogServices = new List<CatalogInternetService>();

        [ObservableProperty]
        private CatalogInternetService _selectedCatalogService;

        [ObservableProperty]
        private IList<CatalogCompanion> _catalogCompanions = new List<CatalogCompanion>();

        [ObservableProperty]
        private IList<X509CertPair> _catalogCertificates = new List<X509CertPair>();

        [ObservableProperty]
        private string _searchKeyword = string.Empty;
    }
}
