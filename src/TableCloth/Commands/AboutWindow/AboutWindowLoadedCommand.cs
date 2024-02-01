using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using System.Threading.Tasks;
using TableCloth.Components;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.AboutWindow;

public sealed class AboutWindowLoadedCommand(
    IResourceResolver resourceResolver,
    ILicenseDescriptor licenseDescriptor) : ViewModelCommandBase<AboutWindowViewModel>, IAsyncCommand<AboutWindowViewModel>
{
    public override void Execute(AboutWindowViewModel viewModel)
        => ExecuteAsync(viewModel).SafeFireAndForget();

    public async Task ExecuteAsync(AboutWindowViewModel viewModel)
    {
        viewModel.AppVersion = Helpers.GetAppVersion();
        viewModel.CatalogDate = resourceResolver.CatalogLastModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? CommonStrings.UnknownText;
        viewModel.LicenseDetails = await licenseDescriptor.GetLicenseDescriptionsAsync();
    }
}
