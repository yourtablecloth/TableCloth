using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TableCloth.Components;

namespace TableCloth.ViewModels
{
    public class AboutWindowViewModel : INotifyPropertyChanged
    {
        public AboutWindowViewModel(
            AppMessageBox appMessageBox,
            CatalogDeserializer catalogDeserializer,
            GitHubReleaseFinder gitHubReleaseFinder,
            LicenseDescriptor licenseDescriptor)
        {
            _appMessageBox = appMessageBox;
            _catalogDeserializer = catalogDeserializer;
            _gitHubReleaseFinder = gitHubReleaseFinder;
            _licenseDescriptor = licenseDescriptor;
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = default)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));

        private readonly AppMessageBox _appMessageBox;
        private readonly CatalogDeserializer _catalogDeserializer;
        private readonly GitHubReleaseFinder _gitHubReleaseFinder;
        private readonly LicenseDescriptor _licenseDescriptor;

        public event PropertyChangedEventHandler PropertyChanged;

        public DateTimeOffset? CatalogVersion
            => _catalogDeserializer.CatalogLastModified;

        public AppMessageBox AppMessageBox
            => _appMessageBox;

        public GitHubReleaseFinder GitHubReleaseFinder
            => _gitHubReleaseFinder;

        public LicenseDescriptor LicenseDescriptor
            => _licenseDescriptor;
    }
}
