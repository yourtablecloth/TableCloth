using TableCloth.Components;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.AboutWindow;

public sealed class AboutWindowLoadedCommand : ViewModelCommandBase<AboutWindowViewModel>
{
    public AboutWindowLoadedCommand(
        IResourceResolver resourceResolver,
        ILicenseDescriptor licenseDescriptor)
    {
        _resourceResolver = resourceResolver;
        _licenseDescriptor = licenseDescriptor;
    }

    private readonly IResourceResolver _resourceResolver;
    private readonly ILicenseDescriptor _licenseDescriptor;

    public override async void Execute(AboutWindowViewModel viewModel)
    {
        viewModel.AppVersion = StringResources.Get_AppVersion();
        viewModel.CatalogDate = _resourceResolver.CatalogLastModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? StringResources.UnknownText;
        viewModel.LicenseDetails = await _licenseDescriptor.GetLicenseDescriptions();
    }
}
