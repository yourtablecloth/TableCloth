using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TableCloth.Contracts;
using TableCloth.Models.Catalog;
using TableCloth.Resources;

namespace TableCloth.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public MainWindowViewModel(
            IAppStartup appStartup,
            IAppUserInterface appUserInterface,
            IAppMessageBox appMessageBox,
            ICatalogDeserializer catalogDeserializer,
            IX509CertPairScanner certPairScanner,
            ISandboxBuilder sandboxBuilder,
            ISandboxLauncher sandboxLauncher)
        {
            _appStartup = appStartup;
            _appUserInterface = appUserInterface;
            _appMessageBox = appMessageBox;
            _catalogDeserializer = catalogDeserializer;
            _certPairScanner = certPairScanner;
            _sandboxBuilder = sandboxBuilder;
            _sandboxLauncher = sandboxLauncher;

            CatalogDocument = _catalogDeserializer.DeserializeCatalog(
                new Uri(StringResources.CatalogUrl, UriKind.Absolute));

            Catalogs = CatalogDocument.Services
                .GroupBy(x => x.Category)
                .Select(x => new SiteCatalogTabViewModel
                {
                    Category = x.Key,
                    Sites = x.ToList(),
                })
                .ToList();
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = default)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));

        private readonly IAppStartup _appStartup;
        private readonly IAppUserInterface _appUserInterface;
        private readonly IAppMessageBox _appMessageBox;
        private readonly ICatalogDeserializer _catalogDeserializer;
        private readonly IX509CertPairScanner _certPairScanner;
        private readonly ISandboxBuilder _sandboxBuilder;
        private readonly ISandboxLauncher _sandboxLauncher;

        private bool _mapNpkiCert;
        private bool _enableMicrophone;
        private bool _enableWebCam;
        private bool _enablePrinters;
        private CatalogDocument _catalogDocument;
        private List<string> _selectedCertFiles;
        private List<SiteCatalogTabViewModel> _catalogs;
        private SiteCatalogTabViewModel _selectedTabView;

        public event PropertyChangedEventHandler PropertyChanged;

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

        public List<string> SelectedCertFiles
        {
            get => _selectedCertFiles;
            set
            {
                if (value != _selectedCertFiles)
                {
                    _selectedCertFiles = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public List<SiteCatalogTabViewModel> Catalogs
        {
            get => _catalogs;
            set
            {
                if (value != _catalogs)
                {
                    _catalogs = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public SiteCatalogTabViewModel SelectedTabView
        {
            get => _selectedTabView;
            set
            {
                if (value != _selectedTabView)
                {
                    _selectedTabView = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public List<string> TemporaryDirectories { get; } = new();
    }
}
