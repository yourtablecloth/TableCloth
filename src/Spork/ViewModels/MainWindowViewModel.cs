using Spork.Commands.MainWindow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace Spork.ViewModels
{
    public class MainWindowViewModelForDesigner : MainWindowViewModel
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

    public class MainWindowViewModel : ViewModelBase
    {
        protected MainWindowViewModel() { }

        public MainWindowViewModel(
            ShowErrorMessageCommand showErrorMessageCommand,
            MainWindowLoadedCommand mainWindowLoadedCommand,
            MainWindowInstallPackagesCommand mainWindowInstallPackagesCommand,
            AboutThisAppCommand aboutThisAppCommand,
            ShowDebugInfoCommand showDebugInfoCommand)
        {
            _showErrorMessageCommand = showErrorMessageCommand;
            _mainWindowLoadedCommand = mainWindowLoadedCommand;
            _mainWindowInstallPackagesCommand = mainWindowInstallPackagesCommand;
            _aboutThisAppCommand = aboutThisAppCommand;
            _showDebugInfoCommand = showDebugInfoCommand;
        }

        private readonly ShowErrorMessageCommand _showErrorMessageCommand;
        private readonly MainWindowLoadedCommand _mainWindowLoadedCommand;
        private readonly MainWindowInstallPackagesCommand _mainWindowInstallPackagesCommand;
        private readonly AboutThisAppCommand _aboutThisAppCommand;
        private readonly ShowDebugInfoCommand _showDebugInfoCommand;

        public ShowErrorMessageCommand ShowErrorMessageCommand
            => _showErrorMessageCommand;

        public MainWindowLoadedCommand MainWindowLoadedCommand
            => _mainWindowLoadedCommand;

        public MainWindowInstallPackagesCommand MainWindowInstallPackagesCommand
            => _mainWindowInstallPackagesCommand;

        public AboutThisAppCommand AboutThisAppCommand
            => _aboutThisAppCommand;

        public ShowDebugInfoCommand ShowDebugInfoCommand
            => _showDebugInfoCommand;

        public event EventHandler WindowLoaded;
        public event EventHandler CloseRequested;

        public async Task NotifyWindowLoadedAsync(object sender, EventArgs e, CancellationToken cancellationToken = default)
            => await TaskFactory.StartNew(() => WindowLoaded?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

        public async Task RequestCloseAsync(object sender, EventArgs e, CancellationToken cancellationToken = default)
            => await TaskFactory.StartNew(() => CloseRequested?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

        private bool _showDryRunNotification;
        private IList<StepItemViewModel> _installSteps = new ObservableCollection<StepItemViewModel>();

        public bool ShowDryRunNotification
        {
            get => _showDryRunNotification;
            set => SetProperty(ref _showDryRunNotification, value);
        }

        public IList<StepItemViewModel> InstallSteps
        {
            get => _installSteps;
            set => SetProperty(ref _installSteps, value);
        }
    }
}
