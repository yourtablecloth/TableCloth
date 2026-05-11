using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
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
            Application application,
            IResourceCacheManager resourceCacheManager,
            IAppUserInterface appUserInterface,
            IVisualThemeManager visualThemeManager,
            ICommandLineArguments commandLineArguments,
            IStepsComposer stepsComposer,
            IStepsPlayer stepsPlayer,
            IAppMessageBox appMessageBox,
            IUserDataStore userDataStore,
            IShortcutCreator shortcutCreator,
            TaskFactory taskFactory)
        {
            _application = application;
            _resourceCacheManager = resourceCacheManager;
            _appUserInterface = appUserInterface;
            _visualThemeManager = visualThemeManager;
            _commandLineArguments = commandLineArguments;
            _stepsComposer = stepsComposer;
            _stepsPlayer = stepsPlayer;
            _appMessageBox = appMessageBox;
            _userDataStore = userDataStore;
            _shortcutCreator = shortcutCreator;
            _taskFactory = taskFactory;
        }

        private readonly Application _application;
        private readonly IResourceCacheManager _resourceCacheManager;
        private readonly IAppUserInterface _appUserInterface;
        private readonly IVisualThemeManager _visualThemeManager;
        private readonly ICommandLineArguments _commandLineArguments;
        private readonly IStepsComposer _stepsComposer;
        private readonly IStepsPlayer _stepsPlayer;
        private readonly IAppMessageBox _appMessageBox;
        private readonly IUserDataStore _userDataStore;
        private readonly IShortcutCreator _shortcutCreator;
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
                var sporkExePath = Assembly.GetExecutingAssembly().Location;
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
        private async Task CatalogItemActivate()
        {
            if (SelectedCatalogService == null)
                return;

            var siteId = SelectedCatalogService.Id;
            await RecordUsageAsync(new[] { siteId });
            var steps = _stepsComposer.ComposeStepsForSites(new[] { siteId });
            await EnterStepsModeAsync(steps, showPrecautions: true);
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
                var window = _appUserInterface.CreatePrecautionsWindow();
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

            ShowReturnToCatalog = false;
            var hasAnyFailure = await _stepsPlayer.PlayStepsAsync(InstallSteps, ShowDryRunNotification);

            if (hasAnyFailure)
                return;

            if (_enteredViaCatalog)
            {
                // 카탈로그를 통해 진입한 경우: 즉시 닫지 않고, 카탈로그로 돌아갈 수 있는 옵션을 제공한다.
                // 사용자는 같은 샌드박스 안에서 추가 사이트를 이어서 사용할 수 있다.
                ShowReturnToCatalog = true;
            }
            else
            {
                // 명령줄(--select)로 들어온 경우: 외부 호출 측이 기대하는 자동 종료 동작을 유지.
                await RequestCloseAsync(this, EventArgs.Empty);
            }
        }

        [RelayCommand]
        private void ReturnToCatalog()
        {
            // 다음 사이트 선택을 위해 설치 상태를 초기화한다. 사용자 데이터(즐겨찾기/사용 기록)는
            // 보존되므로 카탈로그 뷰는 이전 상호작용이 반영된 상태로 다시 보인다.
            InstallSteps = new ObservableCollection<StepItemViewModel>();
            SelectedCatalogService = null;
            ShowReturnToCatalog = false;
            ShowStepsView = false;
            ShowCatalogView = true;
        }

        [RelayCommand]
        private void AboutThisApp()
        {
            var aboutWindow = _appUserInterface.CreateAboutWindow();
            aboutWindow.ShowDialog();
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
        private bool _showReturnToCatalog;

        [ObservableProperty]
        private bool _showFavoritesOnly;

        [ObservableProperty]
        private IList<StepItemViewModel> _installSteps = new ObservableCollection<StepItemViewModel>();

        [ObservableProperty]
        private IList<CatalogInternetService> _catalogServices = new List<CatalogInternetService>();

        [ObservableProperty]
        private CatalogInternetService _selectedCatalogService;

        [ObservableProperty]
        private string _searchKeyword = string.Empty;
    }
}
