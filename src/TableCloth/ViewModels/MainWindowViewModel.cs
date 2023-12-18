using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using TableCloth.Commands;
using TableCloth.Components;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;

namespace TableCloth.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public MainWindowViewModel(
            CatalogDeserializer catalogDeserializer,
            AppRestartManager appRestartManager,
            SandboxCleanupManager sandboxCleanupManager,
            MainWindowLoadedCommand mainWindowLoadedCommand,
            LaunchSandboxCommand launchSandboxCommand,
            CreateShortcutCommand createShortcutCommand,
            AppRestartCommand appRestartCommand,
            AboutThisAppCommand aboutThisAppCommand)
        {
            _appRestartManager = appRestartManager;
            _sandboxCleanupManager = sandboxCleanupManager;
            _mainWindowLoadedCommand = mainWindowLoadedCommand;
            _launchSandboxCommand = launchSandboxCommand;
            _createShortcutCommand = createShortcutCommand;
            _appRestartCommand = appRestartCommand;
            _aboutThisAppCommand = aboutThisAppCommand;

            try
            {
                CatalogDocument = catalogDeserializer.DeserializeCatalog();
                Services = CatalogDocument.Services.ToList();
            }
            catch
            {
                // To Do: Write exception log here
                CatalogDocument = new CatalogDocument();
                Services = Array.Empty<CatalogInternetService>().ToList();
            }
            finally
            {
                CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(this.Services);
                view.Filter = Services_Filter;
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = default)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));

        private readonly AppRestartManager _appRestartManager;
        private readonly SandboxCleanupManager _sandboxCleanupManager;
        private readonly MainWindowLoadedCommand _mainWindowLoadedCommand;
        private readonly LaunchSandboxCommand _launchSandboxCommand;
        private readonly CreateShortcutCommand _createShortcutCommand;
        private readonly AppRestartCommand _appRestartCommand;
        private readonly AboutThisAppCommand _aboutThisAppCommand;

        private string _filterText;
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
        private List<CatalogInternetService> _services;
        private List<CatalogInternetService> _selectedServices;
        private bool _requireRestart;

        public event PropertyChangedEventHandler PropertyChanged;

        public AppRestartManager AppRestartManager
            => _appRestartManager;

        public SandboxCleanupManager SandboxCleanupManager
            => _sandboxCleanupManager;

        public MainWindowLoadedCommand MainWindowLoadedCommand
            => _mainWindowLoadedCommand;

        public LaunchSandboxCommand LaunchSandboxCommand
            => _launchSandboxCommand;

        public CreateShortcutCommand CreateShortcutCommand
            => _createShortcutCommand;

        public AppRestartCommand AppRestartCommand
            => _appRestartCommand;

        public AboutThisAppCommand AboutThisAppCommand
            => _aboutThisAppCommand;

        public string FilterText
        {
            get => _filterText;
            set
            {
                if (value != _filterText)
                {
                    _filterText = value;
                    NotifyPropertyChanged();
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

        public List<CatalogInternetService> SelectedServices
        {
            get => _selectedServices;
            set
            {
                if (value != _selectedServices)
                {
                    _selectedServices = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public bool RequireRestart
        {
            get => _requireRestart;
            set
            {
                if (value != _requireRestart)
                {
                    _requireRestart = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public List<string> TemporaryDirectories { get; } = new();

        public string CurrentDirectory { get; set; }

        public bool HasServices
            => Services != null && Services.Any();

        private bool Services_Filter(object item)
        {
            var filterText = this.FilterText;

            if (string.IsNullOrWhiteSpace(filterText))
                return true;

            var actualItem = item as CatalogInternetService;

            if (actualItem == null)
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
        }
    }
}
