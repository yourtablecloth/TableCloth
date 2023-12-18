using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using TableCloth.Components;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Models.WindowsSandbox;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Commands
{
    public sealed class LaunchSandboxCommand : CommandBase
    {
        public LaunchSandboxCommand(
            SandboxLauncher sandboxLauncher,
            AppMessageBox appMessageBox,
            SharedLocations sharedLocations,
            SandboxBuilder sandboxBuilder,
            SandboxCleanupManager sandboxCleanupManager)
        {
            this.sandboxLauncher = sandboxLauncher;
            this.appMessageBox = appMessageBox;
            this.sharedLocations = sharedLocations;
            this.sandboxBuilder = sandboxBuilder;
            this.sandboxCleanupManager = sandboxCleanupManager;
        }

        private readonly SandboxLauncher sandboxLauncher;
        private readonly AppMessageBox appMessageBox;
        private readonly SharedLocations sharedLocations;
        private readonly SandboxBuilder sandboxBuilder;
        private readonly SandboxCleanupManager sandboxCleanupManager;

        protected override bool EvaluateCanExecute()
            => (!this.sandboxLauncher.IsSandboxRunning());

        public override void Execute(object parameter)
        {
            if (this.sandboxLauncher.IsSandboxRunning())
            {
                this.appMessageBox.DisplayError(StringResources.Error_Windows_Sandbox_Already_Running, false);
                return;
            }

            switch (parameter)
            {
                case MainWindowArgumentModel v1ArgumentModel:
                    this.RunSandboxFromV1ArgumentModel(v1ArgumentModel);
                    break;

                case MainWindowViewModel v1ViewModel:
                    this.RunSandboxFromV1ViewModel(v1ViewModel);
                    break;

                case DetailPageArgumentModel v2ArgumentModel:
                    this.RunSandboxFromV2ArgumentModel(v2ArgumentModel);
                    break;

                case DetailPageViewModel v2ViewModel:
                    this.RunSandboxFromV2ViewModel(v2ViewModel);
                    break;

                case TableClothConfiguration configuration:
                    this.RunSandbox(configuration);
                    break;

                default:
                    throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));
            }
        }

        private void RunSandboxFromV1ArgumentModel(MainWindowArgumentModel argumentModel)
            => RunSandbox(argumentModel.GetTableClothConfiguration());

        private void RunSandboxFromV1ViewModel(MainWindowViewModel viewModel)
        {
            var selectedCert = viewModel.SelectedCertFile;

            if (!viewModel.MapNpkiCert)
                selectedCert = null;

            var config = new TableClothConfiguration()
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
                Companions = viewModel.CatalogDocument.Companions,
                Services = viewModel.SelectedServices,
            };

            RunSandbox(config);
        }

        private void RunSandboxFromV2ArgumentModel(DetailPageArgumentModel argumentModel)
            => RunSandbox(argumentModel.GetTableClothConfiguration());

        private void RunSandboxFromV2ViewModel(DetailPageViewModel viewModel)
        {
            var selectedCert = viewModel.SelectedCertFile;

            if (!viewModel.MapNpkiCert)
                selectedCert = null;

            var config = new TableClothConfiguration()
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
                Services = new[] { viewModel.SelectedService, },
            };

            this.RunSandbox(config);
        }

        private void RunSandbox(TableClothConfiguration config)
        {
            if (config.CertPair != null)
            {
                var now = DateTime.Now;
                var expireWindow = StringResources.Cert_ExpireWindow;

                if (now < config.CertPair.NotBefore)
                    this.appMessageBox.DisplayError(StringResources.Error_Cert_MayTooEarly(now, config.CertPair.NotBefore), false);

                if (now > config.CertPair.NotAfter)
                    this.appMessageBox.DisplayError(StringResources.Error_Cert_Expired, false);
                else if (now > config.CertPair.NotAfter.Add(expireWindow))
                    this.appMessageBox.DisplayInfo(StringResources.Error_Cert_ExpireSoon(now, config.CertPair.NotAfter, expireWindow));
            }

            var tempPath = this.sharedLocations.GetTempPath();
            var excludedFolderList = new List<SandboxMappedFolder>();
            var wsbFilePath = this.sandboxBuilder.GenerateSandboxConfiguration(tempPath, config, excludedFolderList);

            if (excludedFolderList.Any())
                this.appMessageBox.DisplayError(StringResources.Error_HostFolder_Unavailable(excludedFolderList.Select(x => x.HostFolder)), false);

            this.sandboxCleanupManager.SetWorkingDirectory(tempPath);
            this.sandboxLauncher.RunSandbox(wsbFilePath);
        }
    }
}
