using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TableCloth.Commands;
using TableCloth.Components;
using TableCloth.Contracts;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;

namespace TableCloth.ViewModels;

public sealed class DetailPageViewModel : ViewModelBase, IPageExtraArgument, ICertPairSelect, ICanComposeConfiguration
{
    [Obsolete("This constructor should be used only in design time context.")]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public DetailPageViewModel() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public DetailPageViewModel(
        AppUserInterface appUserInterface,
        NavigationService navigationService,
        SharedLocations sharedLocations,
        X509CertPairScanner certPairScanner,
        PreferencesManager preferencesManager,
        AppRestartManager appRestartManager,
        LaunchSandboxCommand launchSandboxCommand,
        CreateShortcutCommand createShortcutCommand,
        CopyCommandLineCommand copyCommandLineCommand,
        CertSelectCommand certSelectCommand)
    {
        _appUserInterface = appUserInterface;
        _navigationService = navigationService;
        _sharedLocations = sharedLocations;
        _certPairScanner = certPairScanner;
        _preferencesManager = preferencesManager;
        _appRestartManager = appRestartManager;
        _launchSandboxCommand = launchSandboxCommand;
        _createShortcutCommand = createShortcutCommand;
        _copyCommandLineCommand = copyCommandLineCommand;
        _certSelectCommand = certSelectCommand;
    }

    private readonly AppUserInterface _appUserInterface;
    private readonly NavigationService _navigationService;
    private readonly SharedLocations _sharedLocations;
    private readonly X509CertPairScanner _certPairScanner;
    private readonly PreferencesManager _preferencesManager;
    private readonly AppRestartManager _appRestartManager;
    private readonly LaunchSandboxCommand _launchSandboxCommand;
    private readonly CreateShortcutCommand _createShortcutCommand;
    private readonly CopyCommandLineCommand _copyCommandLineCommand;
    private readonly CertSelectCommand _certSelectCommand;

    private CatalogInternetService? _selectedService;
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

    public object? ExtraArgument { get; set; }

    public List<string> TemporaryDirectories { get; } = new();

    public string? CurrentDirectory { get; set; }

    public AppUserInterface AppUserInterface
        => _appUserInterface;

    public NavigationService NavigationService
        => _navigationService;

    public SharedLocations SharedLocations
        => _sharedLocations;

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

    public CertSelectCommand CertSelectCommand
        => _certSelectCommand;

    public string? Id
        => _selectedService?.Id;

    public string? DisplayName
        => _selectedService?.DisplayName;

    public string? Url
        => _selectedService?.Url;

    public string? CompatibilityNotes
        => _selectedService?.CompatibilityNotes;

    public int? PackageCountForDisplay
        => _selectedService?.PackageCountForDisplay;

    public ImageSource? ServiceLogo
    {
        get
        {
            var serviceId = this.Id;

            if (string.IsNullOrWhiteSpace(serviceId))
                return null;

            var targetFilePath = Path.Combine(_sharedLocations.GetImageDirectoryPath(), $"{serviceId}.png");

            if (!File.Exists(targetFilePath))
                return null;

            return new BitmapImage(new Uri(targetFilePath));
        }
    }

    public CatalogInternetService? SelectedService
    {
        get => _selectedService;
        set => SetProperty(ref _selectedService, value, new string[] {
            nameof(SelectedService),
            nameof(Id),
            nameof(DisplayName),
            nameof(Url),
            nameof(CompatibilityNotes),
            nameof(PackageCountForDisplay),
            nameof(ServiceLogo),
        });
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
        set => SetProperty(ref _lastDisclaimerAgreedTime, value);
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

    public IEnumerable<CatalogInternetService> SelectedServices
        => _selectedService != null ? new CatalogInternetService[] { _selectedService, } : Enumerable.Empty<CatalogInternetService>();

    public TableClothConfiguration GetTableClothConfiguration()
    {
        var selectedCert = this.SelectedCertFile;

        if (!this.MapNpkiCert)
            selectedCert = null;

        var config = new TableClothConfiguration()
        {
            CertPair = selectedCert,
            EnableMicrophone = this.EnableMicrophone,
            EnableWebCam = this.EnableWebCam,
            EnablePrinters = this.EnablePrinters,
            InstallEveryonesPrinter = this.InstallEveryonesPrinter,
            InstallAdobeReader = this.InstallAdobeReader,
            InstallHancomOfficeViewer = this.InstallHancomOfficeViewer,
            InstallRaiDrive = this.InstallRaiDrive,
            EnableInternetExplorerMode = this.EnableInternetExplorerMode,
            Companions = new CatalogCompanion[] { }, /*ViewModel.CatalogDocument.Companions*/
        };

        if (this.SelectedService != null)
            config.Services = new[] { this.SelectedService };

        return config;
    }
}
