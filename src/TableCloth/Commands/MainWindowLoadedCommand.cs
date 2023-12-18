using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using TableCloth.Components;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands
{
    public sealed class MainWindowLoadedCommand : BaseCommand
    {
        public MainWindowLoadedCommand(
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
            this.visualThemeManager = visualThemeManager;
            this.preferencesManager = preferencesManager;
            this.certPairScanner = certPairScanner;
            this.sharedLocations = sharedLocations;
            this.resourceResolver = resourceResolver;
            this.commandLineParser = commandLineParser;
            this.appMessageBox = appMessageBox;
            this.appRestartManager = appRestartManager;
            this.launchSandboxCommand = launchSandboxCommand;
        }

        private readonly VisualThemeManager visualThemeManager;
        private readonly PreferencesManager preferencesManager;
        private readonly X509CertPairScanner certPairScanner;
        private readonly SharedLocations sharedLocations;
        private readonly ResourceResolver resourceResolver;
        private readonly CommandLineParser commandLineParser;
        private readonly AppMessageBox appMessageBox;
        private readonly AppRestartManager appRestartManager;
        private readonly LaunchSandboxCommand launchSandboxCommand;

        public override async void Execute(object parameter)
        {
            if (parameter is not MainWindowViewModel viewModel)
                throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

            this.visualThemeManager.ApplyAutoThemeChangeToMainWindow();
            var currentConfig = this.preferencesManager.LoadPreferences();

            if (currentConfig == null)
                currentConfig = this.preferencesManager.GetDefaultPreferences();

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

            var foundCandidate = this.certPairScanner.ScanX509Pairs(this.certPairScanner.GetCandidateDirectories()).FirstOrDefault();

            if (foundCandidate != null)
            {
                viewModel.SelectedCertFile = foundCandidate;
                viewModel.MapNpkiCert = true;
            }

            viewModel.PropertyChanged += ViewModel_PropertyChanged;

            if (viewModel.ShouldNotifyDisclaimer)
            {
                var disclaimerWindow = new DisclaimerWindow();
                var result = disclaimerWindow.ShowDialog();

                if (result.HasValue && result.Value)
                    viewModel.LastDisclaimerAgreedTime = DateTime.UtcNow;
            }

            var services = viewModel.Services;
            var directoryPath = this.sharedLocations.GetImageDirectoryPath();

            await this.resourceResolver.LoadSiteImages(services, directoryPath).ConfigureAwait(false);

            // Command Line Parse
            var args = App.Current.Arguments.ToArray();

            if (args.Count() > 0)
            {
                var parsedArg = this.commandLineParser.ParseForV1(args);

                if (parsedArg.ShowCommandLineHelp)
                {
                    this.appMessageBox.DisplayInfo(StringResources.TableCloth_TableCloth_Switches_Help, MessageBoxButton.OK);
                    return;
                }

                if (parsedArg.SelectedServices.Count() > 0)
                    this.launchSandboxCommand.Execute(parsedArg);
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is not MainWindowViewModel viewModel)
                throw new ArgumentException("Selected parameter is not a supported type.", nameof(sender));

            var currentConfig = this.preferencesManager.LoadPreferences();

            if (currentConfig == null)
                currentConfig = this.preferencesManager.GetDefaultPreferences();

            switch (e.PropertyName)
            {
                case nameof(MainWindowViewModel.EnableLogAutoCollecting):
                    currentConfig.UseLogCollection = viewModel.EnableLogAutoCollecting;
                    if (this.appMessageBox.DisplayInfo(StringResources.Ask_RestartRequired, MessageBoxButton.OKCancel).Equals(MessageBoxResult.OK))
                    {
                        this.appRestartManager.ReserveRestart = true;
                        this.appRestartManager.RestartNow();
                    }
                    break;

                case nameof(MainWindowViewModel.V2UIOptIn):
                    currentConfig.V2UIOptIn = viewModel.V2UIOptIn;
                    if (this.appMessageBox.DisplayInfo(StringResources.Ask_RestartRequired, MessageBoxButton.OKCancel).Equals(MessageBoxResult.OK))
                    {
                        this.appRestartManager.ReserveRestart = true;
                        this.appRestartManager.RestartNow();
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

            this.preferencesManager.SavePreferences(currentConfig);
        }
    }
}
