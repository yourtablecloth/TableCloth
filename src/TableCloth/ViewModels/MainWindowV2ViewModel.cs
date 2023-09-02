using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TableCloth.Components;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;

namespace TableCloth.ViewModels
{
    public class MainWindowV2ViewModel : INotifyPropertyChanged
    {
        public MainWindowV2ViewModel(
            SharedLocations sharedLocations,
            AppStartup appStartup,
            AppMessageBox appMessageBox,
            CatalogDeserializer catalogDeserializer,
            X509CertPairScanner certPairScanner,
            SandboxBuilder sandboxBuilder,
            SandboxLauncher sandboxLauncher,
            PreferencesManager preferencesManager,
            ResourceResolver resourceResolver,
            AppRestartManager appRestartManager)
        {
            _sharedLocations = sharedLocations;
            _appStartup = appStartup;
            _appMessageBox = appMessageBox;
            _catalogDeserializer = catalogDeserializer;
            _certPairScanner = certPairScanner;
            _sandboxBuilder = sandboxBuilder;
            _sandboxLauncher = sandboxLauncher;
            _preferencesManager = preferencesManager;
            _resourceResolver = resourceResolver;
            _appRestartManager = appRestartManager;

            try
            {
                CatalogDocument = _catalogDeserializer.DeserializeCatalog();
                Services = CatalogDocument.Services.ToList();
            }
            catch (Exception ex)
            {
                _appMessageBox.DisplayError(ex, false);
                CatalogDocument = new CatalogDocument();
                Services = Array.Empty<CatalogInternetService>().ToList();
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = default)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));

        private readonly SharedLocations _sharedLocations;
        private readonly AppStartup _appStartup;
        private readonly AppMessageBox _appMessageBox;
        private readonly CatalogDeserializer _catalogDeserializer;
        private readonly X509CertPairScanner _certPairScanner;
        private readonly SandboxBuilder _sandboxBuilder;
        private readonly SandboxLauncher _sandboxLauncher;
        private readonly PreferencesManager _preferencesManager;
        private readonly ResourceResolver _resourceResolver;
        private readonly AppRestartManager _appRestartManager;

        private bool _mapNpkiCert;
        private bool _enableLogAutoCollecting;
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
        private List<CatalogInternetService> _services;

        public event PropertyChangedEventHandler PropertyChanged;

        public SharedLocations SharedLocations
            => _sharedLocations;

        public AppStartup AppStartup
            => _appStartup;

        public AppMessageBox AppMessageBox
            => _appMessageBox;

        public CatalogDeserializer CatalogDeserializer
            => _catalogDeserializer;

        public X509CertPairScanner CertPairScanner
            => _certPairScanner;

        public SandboxBuilder SandboxBuilder
            => _sandboxBuilder;

        public SandboxLauncher SandboxLauncher
            => _sandboxLauncher;

        public PreferencesManager PreferencesManager
            => _preferencesManager;

        public ResourceResolver ResourceResolver
            => _resourceResolver;

        public AppRestartManager AppRestartManager
            => _appRestartManager;

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

        public List<CatalogInternetService> Services
        {
            get => _services;
            set
            {
                if (value != _services)
                {
                    _services = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public List<string> TemporaryDirectories { get; } = new();

        public string CurrentDirectory { get; set; }

        public bool HasServices
            => Services != null && Services.Any();
    }
}
