using Hostess.Components;
using Hostess.ViewModels;
using TableCloth.Resources;

namespace Hostess.Commands.AboutWindow
{
    public sealed class AboutWindowLoadedCommand : ViewModelCommandBase<AboutWindowViewModel>
    {
        public AboutWindowLoadedCommand(
            ILicenseDescriptor licenseDescriptor,
            IResourceResolver resourceResolver)
        {
            _licenseDescriptor = licenseDescriptor;
            _resourceResolver = resourceResolver;
        }

        private readonly ILicenseDescriptor _licenseDescriptor;
        private readonly IResourceResolver _resourceResolver;

        public override void Execute(AboutWindowViewModel viewModel)
        {
            viewModel.AppVersion = StringResources.Get_AppVersion();
            viewModel.CatalogVersion = _resourceResolver.CatalogLastModified?.ToString() ?? StringResources.UnknownText;
            viewModel.LicenseDescription = _licenseDescriptor.GetLicenseDescriptions();
        }
    }
}
