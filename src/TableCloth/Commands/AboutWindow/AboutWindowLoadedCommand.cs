using System;
using TableCloth.Components;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.AboutWindow;

public sealed class AboutWindowLoadedCommand : CommandBase
{
    public AboutWindowLoadedCommand(
        CatalogDeserializer catalogDeserializer,
        LicenseDescriptor licenseDescriptor)
    {
        _catalogDeserializer = catalogDeserializer;
        _licenseDescriptor = licenseDescriptor;
    }

    private readonly CatalogDeserializer _catalogDeserializer;
    private readonly LicenseDescriptor _licenseDescriptor;

    public override async void Execute(object? parameter)
    {
        var viewModel = parameter as AboutWindowViewModel;

        if (viewModel == null)
            throw new ArgumentNullException(nameof(viewModel));

        viewModel.AppVersion = StringResources.Get_AppVersion();
        viewModel.CatalogDate = _catalogDeserializer.CatalogLastModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? StringResources.UnknownText;
        viewModel.LicenseDetails = await _licenseDescriptor.GetLicenseDescriptions();
    }
}
