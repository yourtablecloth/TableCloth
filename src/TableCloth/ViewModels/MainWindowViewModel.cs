using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Data;
using TableCloth.Commands;
using TableCloth.Components;
using TableCloth.Contracts;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;

namespace TableCloth.ViewModels
{
    public class MainWindowViewModel : ViewModelBase, ICertSelect
    {
        [Obsolete("This constructor should be used only in design time context.")]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public MainWindowViewModel() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public MainWindowViewModel(
            CatalogDeserializer catalogDeserializer,
            MainWindowLoadedCommand mainWindowLoadedCommand,
            MainWindowClosedCommand mainWindowClosedCommand,
            LaunchSandboxCommand launchSandboxCommand,
            CreateShortcutCommand createShortcutCommand,
            AppRestartCommand appRestartCommand,
            AboutThisAppCommand aboutThisAppCommand,
            CertSelectCommand certSelectCommand)
        {
            _catalogDeserializer = catalogDeserializer;
            _mainWindowLoadedCommand = mainWindowLoadedCommand;
            _mainWindowClosedCommand = mainWindowClosedCommand;
            _launchSandboxCommand = launchSandboxCommand;
            _createShortcutCommand = createShortcutCommand;
            _appRestartCommand = appRestartCommand;
            _aboutThisAppCommand = aboutThisAppCommand;
            _certSelectCommand = certSelectCommand;

            try
            {
                var catalogDoc = _catalogDeserializer.DeserializeCatalog();

                if (catalogDoc == null)
                    catalogDoc = new CatalogDocument();

                CatalogDocument = catalogDoc;

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

        private readonly CatalogDeserializer _catalogDeserializer;
        private readonly MainWindowLoadedCommand _mainWindowLoadedCommand;
        private readonly MainWindowClosedCommand _mainWindowClosedCommand;
        private readonly LaunchSandboxCommand _launchSandboxCommand;
        private readonly CreateShortcutCommand _createShortcutCommand;
        private readonly AppRestartCommand _appRestartCommand;
        private readonly AboutThisAppCommand _aboutThisAppCommand;
        private readonly CertSelectCommand _certSelectCommand;

        private string _filterText = string.Empty;
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
        private CatalogDocument? _catalogDocument;
        private X509CertPair? _selectedCertFile;
        private List<CatalogInternetService> _services = new();
        private List<CatalogInternetService> _selectedServices = new();

        public MainWindowLoadedCommand MainWindowLoadedCommand
            => _mainWindowLoadedCommand;

        public MainWindowClosedCommand MainWindowClosedCommand
            => _mainWindowClosedCommand;

        public LaunchSandboxCommand LaunchSandboxCommand
            => _launchSandboxCommand;

        public CreateShortcutCommand CreateShortcutCommand
            => _createShortcutCommand;

        public AppRestartCommand AppRestartCommand
            => _appRestartCommand;

        public AboutThisAppCommand AboutThisAppCommand
            => _aboutThisAppCommand;

        public CertSelectCommand CertSelectCommand
            => _certSelectCommand;

        public string FilterText
        {
            get => _filterText;
            set => SetProperty(ref _filterText, value);
        }

        public bool MapNpkiCert
        {
            get => _mapNpkiCert;
            set => SetProperty(ref _mapNpkiCert, value);
        }

        public bool EnableLogAutoCollecting
        {
            get => _enableLogAutoCollecting;
            set => SetProperty(ref _enableLogAutoCollecting, value);
        }

        public bool V2UIOptIn
        {
            get => _v2UIOptIn;
            set => SetProperty(ref _v2UIOptIn, value);
        }

        public bool EnableMicrophone
        {
            get => _enableMicrophone;
            set => SetProperty(ref _enableMicrophone, value);
        }

        public bool EnableWebCam
        {
            get => _enableWebCam;
            set => SetProperty(ref _enableWebCam, value);
        }

        public bool EnablePrinters
        {
            get => _enablePrinters;
            set => SetProperty(ref _enablePrinters, value);
        }

        public bool InstallEveryonesPrinter
        {
            get => _installEveryonesPrinter;
            set => SetProperty(ref _installEveryonesPrinter, value);
        }

        public bool InstallAdobeReader
        {
            get => _installAdobeReader;
            set => SetProperty(ref _installAdobeReader, value);
        }

        public bool InstallHancomOfficeViewer
        {
            get => _installHancomOfficeViewer;
            set => SetProperty(ref _installHancomOfficeViewer, value);
        }

        public bool InstallRaiDrive
        {
            get => _installRaiDrive;
            set => SetProperty(ref _installRaiDrive, value);
        }

        public bool EnableInternetExplorerMode
        {
            get => _enableInternetExplorerMode;
            set => SetProperty(ref _enableInternetExplorerMode, value);
        }

        public DateTime? LastDisclaimerAgreedTime
        {
            get => _lastDisclaimerAgreedTime;
            set => SetProperty(ref _lastDisclaimerAgreedTime, value,
                new string[] { nameof(LastDisclaimerAgreedTime), nameof(ShouldNotifyDisclaimer), });
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

        public CatalogDocument? CatalogDocument
        {
            get => _catalogDocument;
            set => SetProperty(ref _catalogDocument, value);
        }

        public X509CertPair? SelectedCertFile
        {
            get => _selectedCertFile;
            set => SetProperty(ref _selectedCertFile, value);
        }

        public List<CatalogInternetService> Services
        {
            get => _services;
            set => SetProperty(ref _services, value);
        }

        public List<CatalogInternetService> SelectedServices
        {
            get => _selectedServices;
            set => SetProperty(ref _selectedServices, value);
        }

        public List<string> TemporaryDirectories { get; } = new List<string>();

        public string? CurrentDirectory { get; set; }

        public bool HasServices
            => Services != null && Services.Any();

        private bool Services_Filter(object item)
        {
            var filterText = this.FilterText;

            if (string.IsNullOrWhiteSpace(filterText))
                return true;

            if (item is not CatalogInternetService actualItem)
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
