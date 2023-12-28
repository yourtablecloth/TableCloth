using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using TableCloth.Components;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands.MainWindow;

public sealed class MainWindowLoadedCommand : CommandBase
{
    public MainWindowLoadedCommand(
        AppUserInterface appUserInterface,
        VisualThemeManager visualThemeManager,
        PreferencesManager preferencesManager,
        X509CertPairScanner certPairScanner,
        SharedLocations sharedLocations,
        ResourceResolver resourceResolver,
        CommandLineParser commandLineParser,
        AppMessageBox appMessageBox,
        AppRestartManager appRestartManager,
        LaunchSandboxCommand launchSandboxCommand)
    {
        _appUserInterface = appUserInterface;
        _visualThemeManager = visualThemeManager;
        _preferencesManager = preferencesManager;
        _certPairScanner = certPairScanner;
        _sharedLocations = sharedLocations;
        _resourceResolver = resourceResolver;
        _commandLineParser = commandLineParser;
        _appMessageBox = appMessageBox;
        _appRestartManager = appRestartManager;
        _launchSandboxCommand = launchSandboxCommand;
    }

    private readonly AppUserInterface _appUserInterface;
    private readonly VisualThemeManager _visualThemeManager;
    private readonly PreferencesManager _preferencesManager;
    private readonly X509CertPairScanner _certPairScanner;
    private readonly SharedLocations _sharedLocations;
    private readonly ResourceResolver _resourceResolver;
    private readonly CommandLineParser _commandLineParser;
    private readonly AppMessageBox _appMessageBox;
    private readonly AppRestartManager _appRestartManager;
    private readonly LaunchSandboxCommand _launchSandboxCommand;

    public override async void Execute(object? parameter)
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

        var services = viewModel.Services;
        var directoryPath = _sharedLocations.GetImageDirectoryPath();

        if (services != null)
            await _resourceResolver.LoadSiteImages(services, directoryPath).ConfigureAwait(false);

        // Command Line Parse
        var args = App.Current.Arguments.ToArray();

        if (args.Count() > 0)
        {
            var parsedArg = _commandLineParser.ParseForV1(args);

            if (parsedArg.ShowCommandLineHelp)
            {
                _appMessageBox.DisplayInfo(StringResources.TableCloth_TableCloth_Switches_Help_V1, MessageBoxButton.OK);
                return;
            }

            if (parsedArg.SelectedServices.Count() > 0)
                if (_launchSandboxCommand.CanExecute(parsedArg))
                    _launchSandboxCommand.Execute(parsedArg);
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
