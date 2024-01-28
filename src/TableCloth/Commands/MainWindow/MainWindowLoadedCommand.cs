using System;
using System.ComponentModel;
using System.Linq;
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
    ICommandLineArguments commandLineArguments) : ViewModelCommandBase<MainWindowViewModel>
{
    public override async void Execute(MainWindowViewModel viewModel)
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
        {
            view.Filter = (item) =>
            {
                var filterText = viewModel.FilterText;

                if (string.IsNullOrWhiteSpace(filterText))
                    return true;

                if (item is not CatalogInternetService actualItem)
                    return true;

                var filterTextSeparators = new char[] { ',', };
                var splittedFilterText = filterText.Split(filterTextSeparators, StringSplitOptions.RemoveEmptyEntries);
                var result = false;

                foreach (var eachFilterText in splittedFilterText)
                {
                    result |= actualItem.DisplayName.Contains(eachFilterText, StringComparison.OrdinalIgnoreCase)
                        || actualItem.CategoryDisplayName.Contains(eachFilterText, StringComparison.OrdinalIgnoreCase)
                        || actualItem.Url.Contains(eachFilterText, StringComparison.OrdinalIgnoreCase)
                        || actualItem.Packages.Count.ToString().Contains(eachFilterText, StringComparison.OrdinalIgnoreCase)
                        || actualItem.Packages.Any(x => x.Name.Contains(eachFilterText, StringComparison.OrdinalIgnoreCase))
                        || actualItem.Id.Contains(eachFilterText, StringComparison.OrdinalIgnoreCase);
                }

                return result;
            };
        }

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

    private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainWindowViewModel viewModel)
            throw new ArgumentException("Selected parameter is not a supported type.", nameof(sender));

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
