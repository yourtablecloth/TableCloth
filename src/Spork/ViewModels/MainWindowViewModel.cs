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
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using TableCloth;
using TableCloth.Models.Catalog;
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
        private readonly TaskFactory _taskFactory;

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

            if (parsedArgs.SelectedServices.Any())
            {
                // 명령줄로 사이트가 지정되어 들어온 경우: 종전대로 즉시 설치 흐름
                await EnterStepsModeAsync(_stepsComposer.ComposeSteps(), showPrecautions: true);
            }
            else
            {
                // 명령줄에 사이트가 없으면: 카탈로그 UI를 보여주고 사용자가 선택하도록 함
                LoadCatalogForBrowsing();
                ShowCatalogView = true;
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

            CatalogServices = ordered;

            var view = (CollectionView)CollectionViewSource.GetDefaultView(CatalogServices);

            if (view != null)
            {
                view.Filter = item => CatalogInternetService.IsMatchedItem(item, SearchKeyword, isFavoriteOnly: false);

                if (!view.GroupDescriptions.Contains(CatalogGroupDescription))
                    view.GroupDescriptions.Add(CatalogGroupDescription);
            }

            PropertyChanged -= ViewModel_PropertyChanged;
            PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(nameof(SearchKeyword), e.PropertyName, StringComparison.Ordinal))
            {
                var view = CollectionViewSource.GetDefaultView(CatalogServices);
                view?.Refresh();
            }
        }

        [RelayCommand]
        private async Task CatalogItemActivate()
        {
            if (SelectedCatalogService == null)
                return;

            var siteId = SelectedCatalogService.Id;
            var steps = _stepsComposer.ComposeStepsForSites(new[] { siteId });
            await EnterStepsModeAsync(steps, showPrecautions: true);
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

            var hasAnyFailure = await _stepsPlayer.PlayStepsAsync(InstallSteps, ShowDryRunNotification);

            if (!hasAnyFailure)
                await RequestCloseAsync(this, EventArgs.Empty);
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
        private IList<StepItemViewModel> _installSteps = new ObservableCollection<StepItemViewModel>();

        [ObservableProperty]
        private IList<CatalogInternetService> _catalogServices = new List<CatalogInternetService>();

        [ObservableProperty]
        private CatalogInternetService _selectedCatalogService;

        [ObservableProperty]
        private string _searchKeyword = string.Empty;
    }
}
