using Hostess.Commands;
using Hostess.Commands.MainWindow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TableCloth;
using TableCloth.ViewModels;

namespace Hostess.ViewModels
{
    public class MainWindowViewModelForDesigner : MainWindowViewModel { }

    public class MainWindowViewModel : ViewModelBase
    {
        protected MainWindowViewModel() { }

        public MainWindowViewModel(
            MainWindowLoadedCommand mainWindowLoadedCommand,
            MainWindowInstallPackagesCommand mainWindowInstallPackagesCommand,
            AboutThisAppCommand aboutThisAppCommand,
            ShowDebugInfoCommand showDebugInfoCommand)
        {
            _mainWindowLoadedCommand = mainWindowLoadedCommand;
            _mainWindowInstallPackagesCommand = mainWindowInstallPackagesCommand;
            _aboutThisAppCommand = aboutThisAppCommand;
            _showDebugInfoCommand = showDebugInfoCommand;
        }

        private readonly MainWindowLoadedCommand _mainWindowLoadedCommand;
        private readonly MainWindowInstallPackagesCommand _mainWindowInstallPackagesCommand;
        private readonly AboutThisAppCommand _aboutThisAppCommand;
        private readonly ShowDebugInfoCommand _showDebugInfoCommand;

        public MainWindowLoadedCommand MainWindowLoadedCommand
            => _mainWindowLoadedCommand;

        public MainWindowInstallPackagesCommand MainWindowInstallPackagesCommand
            => _mainWindowInstallPackagesCommand;

        public AboutThisAppCommand AboutThisAppCommand
            => _aboutThisAppCommand;

        public ShowDebugInfoCommand ShowDebugInfoCommand
            => _showDebugInfoCommand;

        public bool DebugMode
            => Helpers.IsDevelopmentBuild;

        public event EventHandler WindowLoaded;
        public event EventHandler CloseRequested;

        public void NotifyWindowLoaded(object sender, EventArgs e)
            => WindowLoaded?.Invoke(sender, e);

        public void RequestClose(object sender)
            => CloseRequested?.Invoke(sender, EventArgs.Empty);

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
