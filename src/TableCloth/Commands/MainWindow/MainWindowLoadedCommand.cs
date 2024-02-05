using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using TableCloth.Components;
using TableCloth.Models.Catalog;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.MainWindow;

public sealed class MainWindowLoadedCommand(
    IApplicationService applicationService,
    IResourceCacheManager resourceCacheManager,
    IAppUserInterface appUserInterface,
    IPreferencesManager preferencesManager,
    IX509CertPairScanner certPairScanner,
    IAppMessageBox appMessageBox,
    IAppRestartManager appRestartManager,
    IConfigurationComposer configurationComposer,
    ISandboxLauncher sandboxLauncher,
    ICommandLineArguments commandLineArguments) : ViewModelCommandBase<MainWindowViewModel>, IAsyncCommand<MainWindowViewModel>
{
    public override void Execute(MainWindowViewModel viewModel)
        => ExecuteAsync(viewModel).SafeFireAndForget();

    public async Task ExecuteAsync(MainWindowViewModel viewModel)
    {
        applicationService.ApplyCosmeticChangeToMainWindow();

        var currentConfig = await preferencesManager.LoadPreferencesAsync()
            ?? preferencesManager.GetDefaultPreferences();

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

        var doc = resourceCacheManager.CatalogDocument;
        var services = doc.Services;
        viewModel.Services = services;

        var view = (CollectionView)CollectionViewSource.GetDefaultView(viewModel.Services);

        if (view != null)
            view.Filter = (item) => CatalogInternetService.IsMatchedItem(item, viewModel.FilterText);

        // Command Line Parse
        var parsedArg = commandLineArguments.Current;

        if (parsedArg != null)
        {
            if (parsedArg.ShowCommandLineHelp)
            {
                appMessageBox.DisplayInfo(StringResources.TableCloth_TableCloth_Switches_Help, MessageBoxButton.OK);
                return;
            }

            if (parsedArg.SelectedServices.Any())
            {
                var config = configurationComposer.GetConfigurationFromArgumentModel(parsedArg);
                await sandboxLauncher.RunSandboxAsync(config);
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => OnViewModelPropertyChangedAsync(sender, e).SafeFireAndForget();

    private async Task OnViewModelPropertyChangedAsync(object? sender, PropertyChangedEventArgs e)
    {
        var viewModel = sender.EnsureArgumentNotNullWithCast<object, MainWindowViewModel>(
            "Selected parameter is not a supported type.", nameof(sender));

        var currentConfig = await preferencesManager.LoadPreferencesAsync();
        currentConfig ??= preferencesManager.GetDefaultPreferences();

        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.EnableLogAutoCollecting):
                currentConfig.UseLogCollection = viewModel.EnableLogAutoCollecting;
                if (appMessageBox.DisplayInfo(AskStrings.Ask_RestartRequired, MessageBoxButton.OKCancel).Equals(MessageBoxResult.OK))
                {
                    appRestartManager.ReserveRestart();
                    appRestartManager.RestartNow();
                }
                break;

            case nameof(MainWindowViewModel.V2UIOptIn):
                currentConfig.V2UIOptIn = viewModel.V2UIOptIn;
                if (appMessageBox.DisplayInfo(AskStrings.Ask_RestartRequired, MessageBoxButton.OKCancel).Equals(MessageBoxResult.OK))
                {
                    appRestartManager.ReserveRestart();
                    appRestartManager.RestartNow();
                }
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
    }
}
