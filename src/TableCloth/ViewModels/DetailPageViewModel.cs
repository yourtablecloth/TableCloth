using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TableCloth.Commands;
using TableCloth.Components;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;

namespace TableCloth.ViewModels
{
    public sealed class DetailPageViewModel : INotifyPropertyChanged
    {
        public DetailPageViewModel(
            X509CertPairScanner certPairScanner,
            PreferencesManager preferencesManager,
            AppRestartManager appRestartManager,
            LaunchSandboxCommand launchSandboxCommand,
            CreateShortcutCommand createShortcutCommand,
            CopyCommandLineCommand copyCommandLineCommand)
        {
            _certPairScanner = certPairScanner;
            _preferencesManager = preferencesManager;
            _appRestartManager = appRestartManager;
            _launchSandboxCommand = launchSandboxCommand;
            _createShortcutCommand = createShortcutCommand;
            _copyCommandLineCommand = copyCommandLineCommand;
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = default)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));

        private void NotifyPropertiesChanged(params string[] propertiesToNotify)
        {
            if (propertiesToNotify == null)
                return;

            foreach (var eachPropertyName in propertiesToNotify)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(eachPropertyName ?? string.Empty));
        }

        private readonly X509CertPairScanner _certPairScanner;
        private readonly PreferencesManager _preferencesManager;
        private readonly AppRestartManager _appRestartManager;
        private readonly LaunchSandboxCommand _launchSandboxCommand;
        private readonly CreateShortcutCommand _createShortcutCommand;
        private readonly CopyCommandLineCommand _copyCommandLineCommand;

        private CatalogInternetService _selectedService;
        private bool _mapNpkiCert;
        private bool _enableLogAutoCollecting;
        private bool _v2UIOptIn;
        private bool _enableMicrophone;
        private bool _enableWebCam;
        private bool _enablePrinters;
        private bool _installEveryonesPrinter;
        private bool _installAdobeReader;
        private bool _installHancomOfficeViewer;
        private bool _installRaiDrive;
        private bool _enableInternetExplorerMode;
        private DateTime? _lastDisclaimerAgreedTime;
        private CatalogDocument _catalogDocument;
        private X509CertPair _selectedCertFile;

        public event PropertyChangedEventHandler PropertyChanged;

        public X509CertPairScanner CertPairScanner
            => _certPairScanner;

        public PreferencesManager PreferencesManager
            => _preferencesManager;

        public AppRestartManager AppRestartManager
            => _appRestartManager;

        public LaunchSandboxCommand LaunchSandboxCommand
            => _launchSandboxCommand;

        public CreateShortcutCommand CreateShortcutCommand
            => _createShortcutCommand;

        public CopyCommandLineCommand CopyCommandLineCommand
            => _copyCommandLineCommand;

        public string Id
            => _selectedService?.Id;

        public string DisplayName
            => _selectedService?.DisplayName;

        public string Url
            => _selectedService?.Url;

        public string CompatibilityNotes
            => _selectedService?.CompatibilityNotes;

        public int? PackageCountForDisplay
            => _selectedService?.PackageCountForDisplay;

        public CatalogInternetService SelectedService
        {
            get => _selectedService;
            set
            {
                if (value != _selectedService)
                {
                    _selectedService = value;
                    NotifyPropertiesChanged(
                        nameof(SelectedService),
                        nameof(Id),
                        nameof(DisplayName),
                        nameof(Url),
                        nameof(CompatibilityNotes),
                        nameof(PackageCountForDisplay)
                    );
                }
            }
        }

        public bool MapNpkiCert
        {
            get => _mapNpkiCert;
            set
            {
                if (value != _mapNpkiCert)
                {
                    _mapNpkiCert = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool EnableLogAutoCollecting
        {
            get => _enableLogAutoCollecting;
            set
            {
                if (value != _enableLogAutoCollecting)
                {
                    _enableLogAutoCollecting = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool V2UIOptIn
        {
            get => _v2UIOptIn;
            set
            {
                if (value != _v2UIOptIn)
                {
                    _v2UIOptIn = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool EnableMicrophone
        {
            get => _enableMicrophone;
            set
            {
                if (value != _enableMicrophone)
                {
                    _enableMicrophone = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool EnableWebCam
        {
            get => _enableWebCam;
            set
            {
                if (value != _enableWebCam)
                {
                    _enableWebCam = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool EnablePrinters
        {
            get => _enablePrinters;
            set
            {
                if (value != _enablePrinters)
                {
                    _enablePrinters = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool InstallEveryonesPrinter
        {
            get => _installEveryonesPrinter;
            set
            {
                if (value != _installEveryonesPrinter)
                {
                    _installEveryonesPrinter = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool InstallAdobeReader
        {
            get => _installAdobeReader;
            set
            {
                if (value != _installAdobeReader)
                {
                    _installAdobeReader = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool InstallHancomOfficeViewer
        {
            get => _installHancomOfficeViewer;
            set
            {
                if (value != _installHancomOfficeViewer)
                {
                    _installHancomOfficeViewer = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool InstallRaiDrive
        {
            get => _installRaiDrive;
            set
            {
                if (value != _installRaiDrive)
                {
                    _installRaiDrive = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool EnableInternetExplorerMode
        {
            get => _enableInternetExplorerMode;
            set
            {
                if (value != _enableInternetExplorerMode)
                {
                    _enableInternetExplorerMode = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public DateTime? LastDisclaimerAgreedTime
        {
            get => _lastDisclaimerAgreedTime;
            set
            {
                if (value != _lastDisclaimerAgreedTime)
                {
                    _lastDisclaimerAgreedTime = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(ShouldNotifyDisclaimer));
                }
            }
        }

        public bool ShouldNotifyDisclaimer
        {
            get
            {
                if (!_lastDisclaimerAgreedTime.HasValue)
                    return true;

                if ((DateTime.UtcNow - _lastDisclaimerAgreedTime.Value).TotalDays >= 7d)
                    return true;

                return false;
            }
        }

        public CatalogDocument CatalogDocument
        {
            get => _catalogDocument;
            set
            {
                if (value != _catalogDocument)
                {
                    _catalogDocument = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public X509CertPair SelectedCertFile
        {
            get => _selectedCertFile;
            set
            {
                if (value != _selectedCertFile)
                {
                    _selectedCertFile = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public List<string> TemporaryDirectories { get; } = new();

        public string CurrentDirectory { get; set; }
    }
}
