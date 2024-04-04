using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TableCloth.Components;
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

        var foundCandidate = certPairScanner.ScanX509Pairs(certPairScanner.GetCandidateDirectories()).FirstOrDefault();

        if (foundCandidate != null)
        {
            viewModel.SelectedCertFile = foundCandidate;
            viewModel.MapNpkiCert = true;
        }

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
            case nameof(MainWindowViewModel.EnableLogAutoCollecting):
                currentConfig.UseLogCollection = viewModel.EnableLogAutoCollecting;
                reserveRestart = appRestartManager.AskRestart();
                break;

            case nameof(MainWindowViewModel.V2UIOptIn):
                currentConfig.V2UIOptIn = viewModel.V2UIOptIn;
                reserveRestart = appRestartManager.AskRestart();
                break;

            case nameof(MainWindowViewModel.EnableMicrophone):
                currentConfig.UseAudioRedirection = viewModel.EnableMicrophone;
                break;

            case nameof(MainWindowViewModel.EnableWebCam):
                currentConfig.UseVideoRedirection = viewModel.EnableWebCam;
                break;

            case nameof(MainWindowViewModel.EnablePrinters):
                currentConfig.UsePrinterRedirection = viewModel.EnablePrinters;
                break;

            case nameof(MainWindowViewModel.InstallEveryonesPrinter):
                currentConfig.InstallEveryonesPrinter = viewModel.InstallEveryonesPrinter;
                break;

            case nameof(MainWindowViewModel.InstallAdobeReader):
                currentConfig.InstallAdobeReader = viewModel.InstallAdobeReader;
                break;

            case nameof(MainWindowViewModel.InstallHancomOfficeViewer):
                currentConfig.InstallHancomOfficeViewer = viewModel.InstallHancomOfficeViewer;
                break;

            case nameof(MainWindowViewModel.InstallRaiDrive):
                currentConfig.InstallRaiDrive = viewModel.InstallRaiDrive;
                break;

            case nameof(MainWindowViewModel.EnableInternetExplorerMode):
                currentConfig.EnableInternetExplorerMode = viewModel.EnableInternetExplorerMode;
                break;

            case nameof(MainWindowViewModel.LastDisclaimerAgreedTime):
                currentConfig.LastDisclaimerAgreedTime = viewModel.LastDisclaimerAgreedTime;
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
