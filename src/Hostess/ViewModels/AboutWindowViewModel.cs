using Hostess.Commands;
using Hostess.Commands.AboutWindow;
using System;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Events;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace Hostess.ViewModels
{
    public class AboutWindowViewModelForDesigner : AboutWindowViewModel { }

    public class AboutWindowViewModel : ViewModelBase
    {
        protected AboutWindowViewModel() { }

        public AboutWindowViewModel(
            AboutWindowLoadedCommand aboutWindowLoadedCommand,
            AboutWindowCloseCommand aboutWindowCloseCommand,
            OpenAppHomepageCommand openAppHomepageCommand)
        {
            _aboutWindowLoadedCommand = aboutWindowLoadedCommand;
            _aboutWindowCloseCommand = aboutWindowCloseCommand;
            _openAppHomepageCommand = openAppHomepageCommand;
        }

        public event EventHandler<DialogRequestEventArgs> CloseRequested;

        public async Task RequestCloseAsync(object sender, DialogRequestEventArgs e, CancellationToken cancellationToken = default)
            => await TaskFactory.StartNew(() => CloseRequested?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

        private readonly AboutWindowLoadedCommand _aboutWindowLoadedCommand;
        private readonly AboutWindowCloseCommand _aboutWindowCloseCommand;
        private readonly OpenAppHomepageCommand _openAppHomepageCommand;

        public AboutWindowLoadedCommand AboutWindowLoadedCommand
            => _aboutWindowLoadedCommand;

        public AboutWindowCloseCommand AboutWindowCloseCommand
            => _aboutWindowCloseCommand;

        public OpenAppHomepageCommand OpenAppHomepageCommand
            => _openAppHomepageCommand;

        private string _appVersion = CommonStrings.UnknownText;
        private string _catalogVersion = CommonStrings.UnknownText;
        private string _licenseDescription = default;
        private string _appHomepageUrl = default;

        public string AppVersion
        {
            get => _appVersion;
            set => SetProperty(ref _appVersion, value);
        }

        public string CatalogVersion
        {
            get => _catalogVersion;
            set => SetProperty(ref _catalogVersion, value);
        }

        public string LicenseDescription
        {
            get => _licenseDescription;
            set => SetProperty(ref _licenseDescription, value);
        }

        public string AppHomepageUrl
        {
            get => _appHomepageUrl;
            set => SetProperty(ref _appHomepageUrl, value);
        }
    }
}
