using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spork.Commands.AboutWindow;
using System;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Events;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace Spork.ViewModels
{
    public partial class AboutWindowViewModelForDesigner : AboutWindowViewModel { }

    public partial class AboutWindowViewModel : ViewModelBase
    {
        protected AboutWindowViewModel() { }

        public AboutWindowViewModel(
            AboutWindowLoadedCommand aboutWindowLoadedCommand,
            AboutWindowCloseCommand aboutWindowCloseCommand,
            OpenAppHomepageCommand openAppHomepageCommand,
            OpenSponsorPageCommand openSponsorPageCommand)
        {
            _aboutWindowLoadedCommand = aboutWindowLoadedCommand;
            _aboutWindowCloseCommand = aboutWindowCloseCommand;
            _openAppHomepageCommand = openAppHomepageCommand;
            _openSponsorPageCommand = openSponsorPageCommand;
        }

        public event EventHandler<DialogRequestEventArgs> CloseRequested;

        public async Task RequestCloseAsync(object sender, DialogRequestEventArgs e, CancellationToken cancellationToken = default)
            => await TaskFactory.StartNew(() => CloseRequested?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

        [RelayCommand]
        private void AboutWindowLoaded()
        {
            _aboutWindowLoadedCommand.Execute(this);
        }

        private AboutWindowLoadedCommand _aboutWindowLoadedCommand;

        [RelayCommand]
        private void AboutWindowClose()
        {
            _aboutWindowCloseCommand.Execute(this);
        }

        private AboutWindowCloseCommand _aboutWindowCloseCommand;

        [RelayCommand]
        private void OpenAppHomepage()
        {
            _openAppHomepageCommand.Execute(this);
        }

        private OpenAppHomepageCommand _openAppHomepageCommand;

        [RelayCommand]
        private void OpenSponsorPage()
        {
            _openSponsorPageCommand.Execute(this);
        }

        private OpenSponsorPageCommand _openSponsorPageCommand;

        [ObservableProperty]
        private string _appVersion = CommonStrings.UnknownText;

        [ObservableProperty]
        private string _catalogVersion = CommonStrings.UnknownText;

        [ObservableProperty]
        private string _licenseDescription = default;

        [ObservableProperty]
        private string _appHomepageUrl = default;
    }
}
