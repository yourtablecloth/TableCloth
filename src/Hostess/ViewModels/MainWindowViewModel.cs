using Hostess.Commands;
using Hostess.Commands.MainWindow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TableCloth.Events;
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

        public event EventHandler<DialogRequestEventArgs> CloseRequested;

        public void RequestClose(object sender, bool? dialogResult)
            => CloseRequested?.Invoke(sender, new DialogRequestEventArgs(dialogResult));

        private IList<InstallItemViewModel> _installItems
            = new ObservableCollection<InstallItemViewModel>();
        private double _width = 320d;
        private double _height = 480d;
        private double _top = 0d;
        private double _left = 0d;
        private double _minWidth = 320d;
        private double _minHeight = 480d;

        public IList<InstallItemViewModel> InstallItems
        {
            get => _installItems;
            set => SetProperty(ref _installItems, value);
        }

        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        public double Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        public double Top
        {
            get => _top;
            set => SetProperty(ref _top, value);
        }

        public double Left
        {
            get => _left;
            set => SetProperty(ref _left, value);
        }

        public double MinWidth
        {
            get => _minWidth;
            set => SetProperty(ref _minWidth, value);
        }

        public double MinHeight
        {
            get => _minHeight;
            set => SetProperty(ref _minHeight, value);
        }
    }
}
