using TableCloth.Components;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.AboutWindow;

public sealed class AboutWindowLoadedCommand(
    IResourceResolver resourceResolver,
    ILicenseDescriptor licenseDescriptor) : ViewModelCommandBase<AboutWindowViewModel>
{
    public override async void Execute(AboutWindowViewModel viewModel)
    {
        viewModel.AppVersion = StringResources.Get_AppVersion();
        viewModel.CatalogDate = resourceResolver.CatalogLastModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? StringResources.UnknownText;
        viewModel.LicenseDetails = await licenseDescriptor.GetLicenseDescriptions();
    }
}
