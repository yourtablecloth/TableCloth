using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

        [RelayCommand]
        private void ShowErrorMessage()
        {
            _showErrorMessageCommand.Execute(this);
        }

        private ShowErrorMessageCommand _showErrorMessageCommand;

        [RelayCommand]
        private void MainWindowLoaded()
        {
            _mainWindowLoadedCommand.Execute(this);
        }

        private MainWindowLoadedCommand _mainWindowLoadedCommand;

        [RelayCommand]
        private void MainWindowInstallPackages()
        {
            _mainWindowInstallPackagesCommand.Execute(this);
        }

        private MainWindowInstallPackagesCommand _mainWindowInstallPackagesCommand;

        [RelayCommand]
        private void AboutThisApp()
        {
            _aboutThisAppCommand.Execute(this);
        }

        private AboutThisAppCommand _aboutThisAppCommand;

        [RelayCommand]
        private void ShowDebugInfo()
        {
            _showDebugInfoCommand.Execute(this);
        }

        private ShowDebugInfoCommand _showDebugInfoCommand;

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
