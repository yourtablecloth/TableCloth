using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TableCloth.Components;
using TableCloth.Models.Configuration;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.DetailPage;

public sealed class DetailPageLoadedCommand(
    IResourceCacheManager resourceCacheManager,
    IPreferencesManager preferencesManager,
    IX509CertPairScanner certPairScanner,
    IAppRestartManager appRestartManager,
    IAppUserInterface appUserInterface,
    ISharedLocations sharedLocations,
    IConfigurationComposer configurationComposer,
    IAppMessageBox appMessageBox,
    ISandboxLauncher sandboxLauncher) : ViewModelCommandBase<DetailPageViewModel>, IAsyncCommand<DetailPageViewModel>
{
    public override void Execute(DetailPageViewModel viewModel)
        => ExecuteAsync(viewModel).SafeFireAndForget();

    public async Task ExecuteAsync(DetailPageViewModel viewModel)
    {
        if (viewModel.SelectedService == null)
            return;

        var services = resourceCacheManager.CatalogDocument?.Services;
        var selectedServiceId = viewModel.SelectedService.Id;
        var selectedService = services?.Where(x => string.Equals(x.Id, selectedServiceId, StringComparison.Ordinal)).FirstOrDefault();
        viewModel.SelectedService = selectedService;

        var currentConfig = await preferencesManager.LoadPreferencesAsync();
        currentConfig ??= preferencesManager.GetDefaultPreferences();

        viewModel.IsFavorite = currentConfig.Favorites.Contains(selectedServiceId);
        viewModel.EnableLogAutoCollecting = currentConfig.UseLogCollection;
        viewModel.V2UIOptIn = currentConfig.V2UIOptIn;
        viewModel.EnableMicrophone = currentConfig.UseAudioRedirection;
        viewModel.EnableWebCam = currentConfig.UseVideoRedirection;
        viewModel.EnablePrinters = currentConfig.UsePrinterRedirection;
        viewModel.InstallEveryonesPrinter = currentConfig.InstallEveryonesPrinter;
        viewModel.InstallAdobeReader = currentConfig.InstallAdobeReader;
        viewModel.InstallHancomOfficeViewer = currentConfig.InstallHancomOfficeViewer;
        viewModel.InstallRaiDrive = currentConfig.InstallRaiDrive;
        viewModel.EnableInternetExplorerMode = currentConfig.EnableInternetExplorerMode;
        viewModel.LastDisclaimerAgreedTime = currentConfig.LastDisclaimerAgreedTime;

        var targetFilePath = sharedLocations.GetImageFilePath(selectedServiceId);

        if (File.Exists(targetFilePath))
            viewModel.ServiceLogo = resourceCacheManager.GetImage(selectedServiceId);

        var allCerts = certPairScanner.ScanX509Pairs(certPairScanner.GetCandidateDirectories());
        var lastUsedCertHash = currentConfig.LastUsedCertHash;
        var selectedCert = default(X509CertPair?);

        if (!string.IsNullOrWhiteSpace(lastUsedCertHash))
            selectedCert = allCerts.FirstOrDefault(x => string.Equals(lastUsedCertHash, x.CertHash, StringComparison.Ordinal));
        else if (allCerts.Count() < 2)
            selectedCert = allCerts.Where(x => x.IsValid).FirstOrDefault();

        viewModel.MapNpkiCert = (selectedCert != null);
        viewModel.SelectedCertFile = selectedCert;

        viewModel.PropertyChanged += ViewModel_PropertyChanged;

        if (viewModel.ShouldNotifyDisclaimer)
        {
            var disclaimerWindow = appUserInterface.CreateDisclaimerWindow();
            var result = disclaimerWindow.ShowDialog();

            if (result.HasValue && result.Value)
                viewModel.LastDisclaimerAgreedTime = DateTime.UtcNow;
        }

        if (viewModel.CommandLineArgumentModel != null &&
            viewModel.CommandLineArgumentModel.SelectedServices.Any())
        {
            var config = configurationComposer.GetConfigurationFromArgumentModel(viewModel.CommandLineArgumentModel);
            await sandboxLauncher.RunSandboxAsync(config);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => OnViewModelPropertyChangedAsync(sender, e).SafeFireAndForget();

    private async Task OnViewModelPropertyChangedAsync(object? sender, PropertyChangedEventArgs e)
    {
        var viewModel = sender.EnsureArgumentNotNullWithCast<object, DetailPageViewModel>(
            "Selected parameter is not a supported type.", nameof(sender));

        var currentConfig = await preferencesManager.LoadPreferencesAsync();
        currentConfig ??= preferencesManager.GetDefaultPreferences();

        var reserveRestart = false;

        switch (e.PropertyName)
        {
            case nameof(DetailPageViewModel.IsFavorite):
                var serviceId = viewModel.SelectedService?.Id;
                if (!string.IsNullOrWhiteSpace(serviceId))
                    if (!currentConfig.Favorites.Contains(serviceId))
                        currentConfig.Favorites.Add(serviceId);
                break;

            case nameof(DetailPageViewModel.EnableLogAutoCollecting):
                currentConfig.UseLogCollection = viewModel.EnableLogAutoCollecting;
                reserveRestart = appRestartManager.AskRestart();
                break;

            case nameof(DetailPageViewModel.V2UIOptIn):
                currentConfig.V2UIOptIn = viewModel.V2UIOptIn;
                appMessageBox.DisplayInfo(UIStringResources.Announcement_V1UIRetirement);
                reserveRestart = appRestartManager.AskRestart();
                break;

            case nameof(DetailPageViewModel.EnableMicrophone):
                currentConfig.UseAudioRedirection = viewModel.EnableMicrophone;
                break;

            case nameof(DetailPageViewModel.EnableWebCam):
                currentConfig.UseVideoRedirection = viewModel.EnableWebCam;
                break;

            case nameof(DetailPageViewModel.EnablePrinters):
                currentConfig.UsePrinterRedirection = viewModel.EnablePrinters;
                break;

            case nameof(DetailPageViewModel.InstallEveryonesPrinter):
                currentConfig.InstallEveryonesPrinter = viewModel.InstallEveryonesPrinter;
                break;

            case nameof(DetailPageViewModel.InstallAdobeReader):
                currentConfig.InstallAdobeReader = viewModel.InstallAdobeReader;
                break;

            case nameof(DetailPageViewModel.InstallHancomOfficeViewer):
                currentConfig.InstallHancomOfficeViewer = viewModel.InstallHancomOfficeViewer;
                break;

            case nameof(DetailPageViewModel.InstallRaiDrive):
                currentConfig.InstallRaiDrive = viewModel.InstallRaiDrive;
                break;

            case nameof(DetailPageViewModel.EnableInternetExplorerMode):
                currentConfig.EnableInternetExplorerMode = viewModel.EnableInternetExplorerMode;
                break;

            case nameof(DetailPageViewModel.LastDisclaimerAgreedTime):
                currentConfig.LastDisclaimerAgreedTime = viewModel.LastDisclaimerAgreedTime;
                break;

            case nameof(DetailPageViewModel.SelectedCertFile):
                currentConfig.LastUsedCertHash = viewModel.SelectedCertFile?.CertHash ?? string.Empty;
                break;

            default:
                return;
        }

        await preferencesManager.SavePreferencesAsync(currentConfig);

        if (reserveRestart)
        {
            appRestartManager.ReserveRestart();
            await viewModel.RequestCloseAsync(viewModel, e);
        }
    }
}
