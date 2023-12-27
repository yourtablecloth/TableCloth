using System;
using System.IO;
using System.Linq;
using TableCloth.Contracts;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;

namespace TableCloth.Components;

public sealed class ConfigurationComposer
{
    public ConfigurationComposer(
        CatalogCacheManager catalogCacheManager)
    {
        _catalogCacheManager = catalogCacheManager;
    }

    private readonly CatalogCacheManager _catalogCacheManager;

    public TableClothConfiguration GetConfigurationFromViewModel(ITableClothViewModel viewModel)
    {
        var selectedCert = viewModel.SelectedCertFile;

        if (!viewModel.MapNpkiCert)
            selectedCert = null;

        return new TableClothConfiguration()
        {
            CertPair = selectedCert,
            EnableMicrophone = viewModel.EnableMicrophone,
            EnableWebCam = viewModel.EnableWebCam,
            EnablePrinters = viewModel.EnablePrinters,
            InstallEveryonesPrinter = viewModel.InstallEveryonesPrinter,
            InstallAdobeReader = viewModel.InstallAdobeReader,
            InstallHancomOfficeViewer = viewModel.InstallHancomOfficeViewer,
            InstallRaiDrive = viewModel.InstallRaiDrive,
            EnableInternetExplorerMode = viewModel.EnableInternetExplorerMode,
            Companions = new CatalogCompanion[] { }, /*ViewModel.CatalogDocument.Companions*/
            Services = viewModel.SelectedServices.ToList(),
        };
    }

    public TableClothConfiguration GetConfigurationFromArgumentModel(ITableClothArgumentModel argumentModel)
    {
        var certPublicKeyData = new byte[] { };
        var certPrivateKeyData = new byte[] { };
        var certPair = default(X509CertPair);

        if (!string.IsNullOrWhiteSpace(argumentModel.CertPublicKeyPath) &&
            File.Exists(argumentModel.CertPublicKeyPath))
            certPublicKeyData = File.ReadAllBytes(argumentModel.CertPublicKeyPath);

        if (!string.IsNullOrWhiteSpace(argumentModel.CertPrivateKeyPath) &&
            File.Exists(argumentModel.CertPrivateKeyPath))
            certPrivateKeyData = File.ReadAllBytes(argumentModel.CertPrivateKeyPath);

        if (certPublicKeyData.Length > 0 &&
            certPrivateKeyData.Length > 0)
            certPair = new X509CertPair(certPublicKeyData, certPrivateKeyData);

        var selectedServices = _catalogCacheManager.CatalogDocument?.Services
            .Where(x => argumentModel.SelectedServices.Contains(x.Id))
            ?? Enumerable.Empty<CatalogInternetService>();

        return new TableClothConfiguration()
        {
            Services = selectedServices.ToList(),
            EnableMicrophone = argumentModel.EnableMicrophone ?? default,
            EnableWebCam = argumentModel.EnableWebCam ?? default,
            EnablePrinters = argumentModel.EnablePrinters ?? default,
            CertPair = certPair,
            InstallEveryonesPrinter = argumentModel.InstallEveryonesPrinter ?? default,
            InstallAdobeReader = argumentModel.InstallAdobeReader ?? default,
            InstallHancomOfficeViewer = argumentModel.InstallHancomOfficeViewer ?? default,
            InstallRaiDrive = argumentModel.InstallRaiDrive ?? default,
            EnableInternetExplorerMode = argumentModel.EnableInternetExplorerMode ?? default,
        };
    }
}
