using CommunityToolkit.Mvvm.ComponentModel;
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

        [ObservableProperty]
        private AboutWindowLoadedCommand _aboutWindowLoadedCommand;

        [ObservableProperty] 
        private AboutWindowCloseCommand _aboutWindowCloseCommand;

        [ObservableProperty] 
        private OpenAppHomepageCommand _openAppHomepageCommand;

        [ObservableProperty] 
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
