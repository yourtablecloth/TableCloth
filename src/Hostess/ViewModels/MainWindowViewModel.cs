using Hostess.Commands.MainWindow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace Hostess.ViewModels
{
    public class MainWindowViewModelForDesigner : MainWindowViewModel
    {
        public IList<InstallItemViewModel> InstallItemsForDesigner
            => DesignTimeResources.DesignTimePackageInformations.Select((x, i) => new InstallItemViewModel()
            {
                InstallItemType = InstallItemType.DownloadAndInstall,
                TargetSiteName = "Sample Site",
                TargetSiteUrl = "https://www.example.com/",
                PackageName = x.Name,
                PackageUrl = x.Url,
                Arguments = x.Arguments,
                Installed = DesignTimeResources.ConvertToTriState(i),
                StatusMessage = "Status",
                ErrorMessage = DesignTimeResources.GenerateRandomErrorMessage(i),
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
        private IList<InstallItemViewModel> _installItems
            = new ObservableCollection<InstallItemViewModel>();

        public bool ShowDryRunNotification
        {
            get => _showDryRunNotification;
            set => SetProperty(ref _showDryRunNotification, value);
        }

        public IList<InstallItemViewModel> InstallItems
        {
            get => _installItems;
            set => SetProperty(ref _installItems, value);
        }
    }
}
