using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TableCloth.Components;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;

namespace TableCloth.Models.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public MainWindowViewModel(
            SharedLocations sharedLocations,
            AppStartup appStartup,
            AppUserInterface appUserInterface,
            AppMessageBox appMessageBox,
            CatalogDeserializer catalogDeserializer,
            X509CertPairScanner certPairScanner,
            SandboxBuilder sandboxBuilder,
            SandboxLauncher sandboxLauncher,
            Preferences preferences)
        {
            _sharedLocations = sharedLocations;
            _appStartup = appStartup;
            _appUserInterface = appUserInterface;
            _appMessageBox = appMessageBox;
            _catalogDeserializer = catalogDeserializer;
            _certPairScanner = certPairScanner;
            _sandboxBuilder = sandboxBuilder;
            _sandboxLauncher = sandboxLauncher;
            _preferences = preferences;

            try
            {
                CatalogDocument = _catalogDeserializer.DeserializeCatalog();
                Services = CatalogDocument.Services.ToList();
                IEModeListDocument = _catalogDeserializer.DeserializeIEModeList();
            }
            catch (Exception ex)
            {
                _appMessageBox.DisplayError(_appUserInterface.MainWindowHandle, ex, false);
                CatalogDocument = new CatalogDocument();
                Services = Array.Empty<CatalogInternetService>().ToList();
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = default)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));

        private readonly SharedLocations _sharedLocations;
        private readonly AppStartup _appStartup;
        private readonly AppUserInterface _appUserInterface;
        private readonly AppMessageBox _appMessageBox;
        private readonly CatalogDeserializer _catalogDeserializer;
        private readonly X509CertPairScanner _certPairScanner;
        private readonly SandboxBuilder _sandboxBuilder;
        private readonly SandboxLauncher _sandboxLauncher;
        private readonly Preferences _preferences;

        private bool _mapNpkiCert;
        private bool _enableLogAutoCollecting;
        private bool _enableMicrophone;
        private bool _enableWebCam;
        private bool _enablePrinters;
        private bool _enableEveryonesPrinter;
        private bool _enableAdobeReader;
        private bool _enableHancomOfficeViewer;
        private bool _enableRaiDrive;
        private bool _enableInternetExplorerMode;
        private DateTime? _lastDisclaimerAgreedTime;
        private CatalogDocument _catalogDocument;
        private IEModeListDocument _ieModeListDocument;
        private X509CertPair _selectedCertFile;
        private List<CatalogInternetService> _services;

        public event PropertyChangedEventHandler PropertyChanged;

        public SharedLocations SharedLocations
            => _sharedLocations;

        public AppStartup AppStartup
            => _appStartup;

        public AppUserInterface AppUserInterface
            => _appUserInterface;

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

        public Preferences Preferences
            => _preferences;

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

        public bool EnableEveryonesPrinter
        {
            get => _enableEveryonesPrinter;
            set
            {
                if (value != _enableEveryonesPrinter)
                {
                    _enableEveryonesPrinter = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool EnableAdobeReader
        {
            get => _enableAdobeReader;
            set
            {
                if (value != _enableAdobeReader)
                {
                    _enableAdobeReader = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool EnableHancomOfficeViewer
        {
            get => _enableHancomOfficeViewer;
            set
            {
                if (value != _enableHancomOfficeViewer)
                {
                    _enableHancomOfficeViewer = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool EnableRaiDrive
        {
            get => _enableRaiDrive;
            set
            {
                if (value != _enableRaiDrive)
                {
                    _enableRaiDrive = value;
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

        public IEModeListDocument IEModeListDocument
        {
            get => _ieModeListDocument;
            set
            {
                if (value != _ieModeListDocument)
                {
                    _ieModeListDocument = value;
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
