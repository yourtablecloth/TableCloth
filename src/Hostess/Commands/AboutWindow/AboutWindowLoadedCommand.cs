using Hostess.Components;
using Hostess.ViewModels;
using TableCloth.Resources;

namespace Hostess.Commands.AboutWindow
{
    public sealed class AboutWindowLoadedCommand : ViewModelCommandBase<AboutWindowViewModel>
    {
        public AboutWindowLoadedCommand(
            SharedProperties sharedProperties,
            LicenseDescriptor licenseDescriptor)
        {
            _sharedProperties = sharedProperties;
            _licenseDescriptor = licenseDescriptor;
        }

        private readonly SharedProperties _sharedProperties;
        private readonly LicenseDescriptor _licenseDescriptor;

        public override void Execute(AboutWindowViewModel viewModel)
        {
            viewModel.AppVersion = StringResources.Get_AppVersion();
            viewModel.CatalogVersion = _sharedProperties.GetCatalogLastModified();
            viewModel.LicenseDescription = _licenseDescriptor.GetLicenseDescriptions();
        }
    }
}
