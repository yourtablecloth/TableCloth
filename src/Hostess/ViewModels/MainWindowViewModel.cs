using Hostess.Commands;
using Hostess.Commands.MainWindow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            AboutThisAppCommand aboutThisAppCommand)
        {
            _mainWindowLoadedCommand = mainWindowLoadedCommand;
            _mainWindowInstallPackagesCommand = mainWindowInstallPackagesCommand;
            _aboutThisAppCommand = aboutThisAppCommand;
        }

        private readonly MainWindowLoadedCommand _mainWindowLoadedCommand;
        private readonly MainWindowInstallPackagesCommand _mainWindowInstallPackagesCommand;
        private readonly AboutThisAppCommand _aboutThisAppCommand;

        public MainWindowLoadedCommand MainWindowLoadedCommand
            => _mainWindowLoadedCommand;

        public MainWindowInstallPackagesCommand MainWindowInstallPackagesCommand
            => _mainWindowInstallPackagesCommand;

        public AboutThisAppCommand AboutThisAppCommand
            => _aboutThisAppCommand;

        public event EventHandler WindowLoaded;
        public event EventHandler CloseRequested;

        public void NotifyWindowLoaded(object sender, EventArgs e)
            => WindowLoaded?.Invoke(sender, e);

        public void RequestClose(object sender)
            => CloseRequested?.Invoke(sender, EventArgs.Empty);

        private IList<InstallItemViewModel> _installItems
            = new ObservableCollection<InstallItemViewModel>();

        public IList<InstallItemViewModel> InstallItems
        {
            get => _installItems;
            set => SetProperty(ref _installItems, value);
        }
    }
}
