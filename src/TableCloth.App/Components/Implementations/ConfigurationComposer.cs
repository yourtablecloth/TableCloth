using System;
using System.IO;
using System.Linq;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.ViewModels;

namespace TableCloth.Components.Implementations;

public sealed class ConfigurationComposer(
    IResourceCacheManager resourceCacheManager) : IConfigurationComposer
{
    public TableClothConfiguration GetConfigurationFromViewModel(DetailPageViewModel viewModel)
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
            EnableSandboxGpuAcceleration = viewModel.EnableSandboxGpuAcceleration,
            Companions = Array.Empty<CatalogCompanion>(),
            Services = viewModel.SelectedServices.ToList(),
            MappedFolders = viewModel.MappedFolders.ToList(),
            // 딥링크로 진입한 경우에만 값이 있다. 이 시점의 값은 이미 카탈로그 도메인 게이트를 통과했다.
            TargetUrl = viewModel.CommandLineArgumentModel?.TargetUrl,
        };
    }

    public TableClothConfiguration GetConfigurationFromArgumentModel(CommandLineArgumentModel argumentModel)
    {
        var certPublicKeyData = Array.Empty<byte>();
        var certPrivateKeyData = Array.Empty<byte>();
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

        var selectedServices = resourceCacheManager.CatalogDocument?.Services
            .Where(x => argumentModel.SelectedServices.Contains(x.Id))
            ?? Enumerable.Empty<CatalogInternetService>();

        return new TableClothConfiguration()
        {
            Services = selectedServices.ToList(),
            EnableMicrophone = argumentModel.EnableMicrophone ?? default,
            EnableWebCam = argumentModel.EnableWebCam ?? default,
            EnablePrinters = argumentModel.EnablePrinters ?? default,
            CertPair = certPair,
            MappedFolders = argumentModel.MappedFolders?.ToList() ?? new(),
            TargetUrl = argumentModel.TargetUrl,
        };
    }
}
