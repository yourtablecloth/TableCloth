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
        private readonly IShortcutCreator _shortcutCreator;
        private readonly IWebBrowserServiceFactory _webBrowserServiceFactory;
        private readonly IX509CertScanner _certScanner;
        private readonly TaskFactory _taskFactory;

        /// <summary>
        /// 사용자가 카탈로그 UI를 통해 진입했는지(true) 또는 명령줄 --select로 진입했는지(false).
        /// 카탈로그 진입의 경우 설치 완료 후 "카탈로그로 돌아가기" UX를 제공한다.
        /// </summary>
        private bool _enteredViaCatalog;

        private SporkUserData _userData = new SporkUserData();
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

            _userData = await _userDataStore.LoadAsync();
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

                await _shortcutCreator.CreateShortcutOnDesktopAsync(
                    destinationPath: sporkExePath,
                    linkName: UIStringResources.Spork_ShortcutLinkName,
                    iconFilePath: sporkExePath,
                    description: UIStringResources.Spork_ShortcutDescription).ConfigureAwait(false);
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

            // 샌드박스의 NPKI 폴더(시작 스크립트가 호스트의 NPKI를 xcopy해 둠)를 스캔하여
            // 현재 사용 가능한 인증서를 카탈로그 탭에서 보여준다. 만료된 인증서도 포함되며
            // UI는 취소선으로 구분한다.
            try
            {
                CatalogCertificates = _certScanner.ScanSandboxNpkiCertificates().ToList();
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
                _ = _userDataStore.SaveAsync(_userData);
            }
        }

        [RelayCommand]
        private async Task ToggleFavorite(CatalogInternetService service)
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

            await _userDataStore.SaveAsync(_userData);

            // 필터가 ShowFavoritesOnly일 때 즉시 반영
            var view = CollectionViewSource.GetDefaultView(CatalogServices);
            view?.Refresh();
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
                using (var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(pair.PublicKey))
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

            // 모달 동안 사용자가 카탈로그를 다시 조작하지 못하도록 선택값을 잠시 보존하되,
            // 모달이 닫힌 뒤에는 다음 사이트 선택을 위해 초기화한다.
            var siteId = SelectedCatalogService.Id;
            var siteUrl = SelectedCatalogService.Url;

            await RecordUsageAsync(new[] { siteId });
            var steps = _stepsComposer.ComposeStepsForSites(new[] { siteId }).ToList();

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
            // 어떤 사이트를 준비하는지 사용자가 알 수 있도록 표시명과 아이콘 키를 함께 전달한다.
            var installWindow = _appUserInterface.CreateInstallStepsWindow(
                steps,
                ShowDryRunNotification,
                targetTitle: SelectedCatalogService.DisplayName,
                targetIconKey: siteId);
            var result = installWindow.ShowDialog();

            // 카탈로그 뷰는 모달 뒤에서 계속 보였으므로 별도 복귀 처리는 필요 없다.
            // 다음 사이트를 자유롭게 고를 수 있도록 선택만 초기화한다.
            SelectedCatalogService = null;

            if (result == true && !string.IsNullOrWhiteSpace(siteUrl))
            {
                // 설치 성공: 대상 사이트를 브라우저로 열어 사용자가 바로 진행할 수 있게 한다.
                TryOpenSiteUrls(new[] { siteUrl });
            }
        }

        private async Task RecordUsageAsync(IEnumerable<string> siteIds)
        {
            var now = DateTime.UtcNow;
            _userData.LastUsedAt ??= new Dictionary<string, DateTime>();

            foreach (var id in siteIds ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                _userData.LastUsedAt[id] = now;
            }

            await _userDataStore.SaveAsync(_userData);
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
