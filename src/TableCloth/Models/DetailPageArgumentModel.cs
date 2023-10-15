using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Resources;

namespace TableCloth.Models
{
    public sealed class DetailPageArgumentModel
    {
        public DetailPageArgumentModel(
            CatalogInternetService selectedService,
            bool builtFromCommandLine,
            bool? enableMicrophone = default,
            bool? enableWebCam = default,
            bool? enablePrinters = default,
            string certPrivateKeyPath = default,
            string certPublicKeyPath = default,
            bool? installEveryonesPrinter = default,
            bool? installAdobeReader = default,
            bool? installHancomOfficeViewer = default,
            bool? installRaiDrive = default,
            bool? enableInternetExplorerMode = default,
            bool showCommandLineHelp = default,
            string currentSearchString = default)
        {
            SelectedService = selectedService;
            EnableMicrophone = enableMicrophone;
            EnableWebCam = enableWebCam;
            EnablePrinters = enablePrinters;
            CertPrivateKeyPath = certPrivateKeyPath;
            CertPublicKeyPath = certPublicKeyPath;
            InstallEveryonesPrinter = installEveryonesPrinter;
            InstallAdobeReader = installAdobeReader;
            InstallHancomOfficeViewer = installHancomOfficeViewer;
            InstallRaiDrive = installRaiDrive;
            EnableInternetExplorerMode = enableInternetExplorerMode;
            ShowCommandLineHelp = showCommandLineHelp;
            BuiltFromCommandLine = builtFromCommandLine;
            CurrentSearchString = currentSearchString;
        }

        public bool BuiltFromCommandLine { get; private set; } = false;

        public CatalogInternetService SelectedService { get; private set; } = default;

        public bool? EnableMicrophone { get; private set; }

        public bool? EnableWebCam { get; private set; }

        public bool? EnablePrinters { get; private set; }

        public string CertPrivateKeyPath { get; private set; }

        public string CertPublicKeyPath { get; private set; }

        public bool? InstallEveryonesPrinter { get; private set; }

        public bool? InstallAdobeReader { get; private set; }

        public bool? InstallHancomOfficeViewer { get; private set; }

        public bool? InstallRaiDrive { get; private set; }

        public bool? EnableInternetExplorerMode { get; private set; }

        public bool ShowCommandLineHelp { get; private set; }

        public string CurrentSearchString { get; private set; }

        public TableClothConfiguration GetTableClothConfiguration()
        {
            var certPublicKeyData = new byte[] { };
            var certPrivateKeyData = new byte[] { };
            var certPair = default(X509CertPair);

            if (!string.IsNullOrWhiteSpace(CertPublicKeyPath) &&
                File.Exists(CertPublicKeyPath))
                certPublicKeyData = File.ReadAllBytes(CertPublicKeyPath);

            if (!string.IsNullOrWhiteSpace(CertPrivateKeyPath) &&
                File.Exists(CertPrivateKeyPath))
                certPrivateKeyData = File.ReadAllBytes(CertPrivateKeyPath);

            if (certPublicKeyData.Length > 0 &&
                certPrivateKeyData.Length > 0)
                certPair = new X509CertPair(certPublicKeyData, certPrivateKeyData);

            return new TableClothConfiguration()
            {
                Services = new[] { SelectedService },
                EnableMicrophone = EnableMicrophone ?? default,
                EnableWebCam = EnableWebCam ?? default,
                EnablePrinters = EnablePrinters ?? default,
                CertPair = certPair,
                InstallEveryonesPrinter = InstallEveryonesPrinter ?? default,
                InstallAdobeReader = InstallAdobeReader ?? default,
                InstallHancomOfficeViewer = InstallHancomOfficeViewer ?? default,
                InstallRaiDrive = InstallRaiDrive ?? default,
                EnableInternetExplorerMode = EnableInternetExplorerMode ?? default,
            };
        }
    }
}
