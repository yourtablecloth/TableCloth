using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TableCloth.Contracts;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;

namespace TableCloth.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public MainWindowViewModel(
            ISharedLocations sharedLocations,
            IAppStartup appStartup,
            IAppUserInterface appUserInterface,
            IAppMessageBox appMessageBox,
            ICatalogDeserializer catalogDeserializer,
            IX509CertPairScanner certPairScanner,
            ISandboxBuilder sandboxBuilder,
            ISandboxLauncher sandboxLauncher,
            IPreferences preferences)
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

        private readonly ISharedLocations _sharedLocations;
        private readonly IAppStartup _appStartup;
        private readonly IAppUserInterface _appUserInterface;
        private readonly IAppMessageBox _appMessageBox;
        private readonly ICatalogDeserializer _catalogDeserializer;
        private readonly IX509CertPairScanner _certPairScanner;
        private readonly ISandboxBuilder _sandboxBuilder;
        private readonly ISandboxLauncher _sandboxLauncher;
        private readonly IPreferences _preferences;

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

        public ISharedLocations SharedLocations
            => _sharedLocations;

        public IAppStartup AppStartup
            => _appStartup;

        public IAppUserInterface AppUserInterface
            => _appUserInterface;

        public IAppMessageBox AppMessageBox
            => _appMessageBox;

        public ICatalogDeserializer CatalogDeserializer
            => _catalogDeserializer;

        public IX509CertPairScanner CertPairScanner
            => _certPairScanner;

        public ISandboxBuilder SandboxBuilder
            => _sandboxBuilder;

        public ISandboxLauncher SandboxLauncher
            => _sandboxLauncher;

        public IPreferences Preferences
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
