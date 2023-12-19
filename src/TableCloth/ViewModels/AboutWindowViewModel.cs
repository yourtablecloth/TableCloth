using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TableCloth.Components;

namespace TableCloth.ViewModels
{
    public class AboutWindowViewModel : ViewModelBase
    {
        [Obsolete("This constructor should be used only in design time context.")]
        public AboutWindowViewModel() { }

        public AboutWindowViewModel(
            AppMessageBox appMessageBox,
            CatalogDeserializer catalogDeserializer,
            ResourceResolver gitHubReleaseFinder,
            LicenseDescriptor licenseDescriptor)
        {
            _appMessageBox = appMessageBox;
            _catalogDeserializer = catalogDeserializer;
            _gitHubReleaseFinder = gitHubReleaseFinder;
            _licenseDescriptor = licenseDescriptor;
        }

        private readonly AppMessageBox _appMessageBox;
        private readonly CatalogDeserializer _catalogDeserializer;
        private readonly ResourceResolver _gitHubReleaseFinder;
        private readonly LicenseDescriptor _licenseDescriptor;

        public DateTimeOffset? CatalogVersion
            => _catalogDeserializer.CatalogLastModified;

        public AppMessageBox AppMessageBox
            => _appMessageBox;

        public ResourceResolver GitHubReleaseFinder
            => _gitHubReleaseFinder;

        public LicenseDescriptor LicenseDescriptor
            => _licenseDescriptor;
    }
}
