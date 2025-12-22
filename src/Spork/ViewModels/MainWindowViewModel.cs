using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spork.Components;
using Spork.Steps;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TableCloth;
using TableCloth.Models.Catalog;
using TableCloth.Resources;
using TableCloth.ViewModels;

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
    }

    public partial class MainWindowViewModel : ViewModelBase
    {
        protected MainWindowViewModel() { }

        public MainWindowViewModel(
            Application application,
            IResourceCacheManager resourceCacheManager,
            IAppUserInterface appUserInterface,
            IVisualThemeManager visualThemeManager,
            ICommandLineArguments commandLineArguments,
            IStepsComposer stepsComposer,
            IStepsPlayer stepsPlayer,
            IAppMessageBox appMessageBox)
        {
            _application = application;
            _resourceCacheManager = resourceCacheManager;
            _appUserInterface = appUserInterface;
            _visualThemeManager = visualThemeManager;
            _commandLineArguments = commandLineArguments;
            _stepsComposer = stepsComposer;
            _stepsPlayer = stepsPlayer;
            _appMessageBox = appMessageBox;
        }

        private readonly Application _application;
        private readonly IResourceCacheManager _resourceCacheManager;
        private readonly IAppUserInterface _appUserInterface;
        private readonly IVisualThemeManager _visualThemeManager;
        private readonly ICommandLineArguments _commandLineArguments;
        private readonly IStepsComposer _stepsComposer;
        private readonly IStepsPlayer _stepsPlayer;
        private readonly IAppMessageBox _appMessageBox;

        [RelayCommand]
        private void ShowErrorMessage()
        {
            _appMessageBox.DisplayError(Convert.ToString(this), true);
        }

        [RelayCommand]
        private async Task MainWindowLoaded()
        {
            _visualThemeManager.ApplyAutoThemeChange(_application.MainWindow);

            var parsedArgs = _commandLineArguments.GetCurrent();
            var catalog = _resourceCacheManager.CatalogDocument;
            var targets = parsedArgs.SelectedServices;

            ShowDryRunNotification = parsedArgs.DryRun;

            await NotifyWindowLoadedAsync(this, EventArgs.Empty);

            var steps = _stepsComposer.ComposeSteps();
            InstallSteps = new ObservableCollection<StepItemViewModel>(steps);

            if (SharedExtensions.HasAnyCompatNotes(catalog, targets))
            {
                var window = _appUserInterface.CreatePrecautionsWindow();
                window.ShowDialog();
            }

            MainWindowInstallPackagesCommand.Execute(this);
        }

        [RelayCommand]
        private async Task MainWindowInstallPackages()
        {
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
            => await TaskFactory.StartNew(() => WindowLoaded?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

        public async Task RequestCloseAsync(object sender, EventArgs e, CancellationToken cancellationToken = default)
            => await TaskFactory.StartNew(() => CloseRequested?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

        [ObservableProperty]
        private bool _showDryRunNotification;

        [ObservableProperty]
        private IList<StepItemViewModel> _installSteps = new ObservableCollection<StepItemViewModel>();
    }
}
