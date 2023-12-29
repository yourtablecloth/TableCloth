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

public sealed class MainWindowLoadedCommand : CommandBase
{
    public MainWindowLoadedCommand(
        ResourceCacheManager resourceCacheManager,
        AppUserInterface appUserInterface,
        VisualThemeManager visualThemeManager,
        PreferencesManager preferencesManager,
        X509CertPairScanner certPairScanner,
        SharedLocations sharedLocations,
        CommandLineParser commandLineParser,
        AppMessageBox appMessageBox,
        AppRestartManager appRestartManager,
        ConfigurationComposer configurationComposer,
        SandboxLauncher sandboxLauncher)
    {
        _resourceCacheManager = resourceCacheManager;
        _appUserInterface = appUserInterface;
        _visualThemeManager = visualThemeManager;
        _preferencesManager = preferencesManager;
        _certPairScanner = certPairScanner;
        _sharedLocations = sharedLocations;
        _commandLineParser = commandLineParser;
        _appMessageBox = appMessageBox;
        _appRestartManager = appRestartManager;
        _configurationComposer = configurationComposer;
        _sandboxLauncher = sandboxLauncher;
    }

    private readonly ResourceCacheManager _resourceCacheManager;
    private readonly AppUserInterface _appUserInterface;
    private readonly VisualThemeManager _visualThemeManager;
    private readonly PreferencesManager _preferencesManager;
    private readonly X509CertPairScanner _certPairScanner;
    private readonly SharedLocations _sharedLocations;
    private readonly CommandLineParser _commandLineParser;
    private readonly AppMessageBox _appMessageBox;
    private readonly AppRestartManager _appRestartManager;
    private readonly ConfigurationComposer _configurationComposer;
    private readonly SandboxLauncher _sandboxLauncher;

    public override void Execute(object? parameter)
    {
        if (parameter is not MainWindowViewModel viewModel)
            throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

        _visualThemeManager.ApplyAutoThemeChange(
            Application.Current.MainWindow);

        var currentConfig = _preferencesManager.LoadPreferences();

        if (currentConfig == null)
            currentConfig = _preferencesManager.GetDefaultPreferences();

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

        var foundCandidate = _certPairScanner.ScanX509Pairs(_certPairScanner.GetCandidateDirectories()).FirstOrDefault();

        if (foundCandidate != null)
        {
            viewModel.SelectedCertFile = foundCandidate;
            viewModel.MapNpkiCert = true;
        }

        viewModel.PropertyChanged += ViewModel_PropertyChanged;

        if (viewModel.ShouldNotifyDisclaimer)
        {
            var disclaimerWindow = _appUserInterface.CreateDisclaimerWindow();
            var result = disclaimerWindow.ShowDialog();

            if (result.HasValue && result.Value)
                viewModel.LastDisclaimerAgreedTime = DateTime.UtcNow;
        }

        var doc = _resourceCacheManager.CatalogDocument;
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

                var splittedFilterText = filterText.Split(new char[] { ',', }, StringSplitOptions.RemoveEmptyEntries);
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

        var directoryPath = _sharedLocations.GetImageDirectoryPath();

        // Command Line Parse
        var parsedArg = _commandLineParser.ParseFromArgv();

        if (parsedArg != null)
        {
            if (parsedArg.ShowCommandLineHelp)
            {
                _appMessageBox.DisplayInfo(StringResources.TableCloth_TableCloth_Switches_Help, MessageBoxButton.OK);
                return;
            }

            if (parsedArg.SelectedServices.Count() > 0)
            {
                var config = _configurationComposer.GetConfigurationFromArgumentModel(parsedArg);
                _sandboxLauncher.RunSandbox(config);
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainWindowViewModel viewModel)
            throw new ArgumentException("Selected parameter is not a supported type.", nameof(sender));

        var currentConfig = _preferencesManager.LoadPreferences();

        if (currentConfig == null)
            currentConfig = _preferencesManager.GetDefaultPreferences();

        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.EnableLogAutoCollecting):
                currentConfig.UseLogCollection = viewModel.EnableLogAutoCollecting;
                if (_appMessageBox.DisplayInfo(StringResources.Ask_RestartRequired, MessageBoxButton.OKCancel).Equals(MessageBoxResult.OK))
                {
                    _appRestartManager.ReserveRestart = true;
                    _appRestartManager.RestartNow();
                }
                break;

            case nameof(MainWindowViewModel.V2UIOptIn):
                currentConfig.V2UIOptIn = viewModel.V2UIOptIn;
                if (_appMessageBox.DisplayInfo(StringResources.Ask_RestartRequired, MessageBoxButton.OKCancel).Equals(MessageBoxResult.OK))
                {
                    _appRestartManager.ReserveRestart = true;
                    _appRestartManager.RestartNow();
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

        _preferencesManager.SavePreferences(currentConfig);
    }
}
