using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using TableCloth.Commands;
using TableCloth.Commands.DetailPage;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public class DetailPageViewModelForDesigner : DetailPageViewModel { }

public class DetailPageViewModel : ViewModelBase, ITableClothViewModel
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    protected DetailPageViewModel() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public DetailPageViewModel(
        DetailPageLoadedCommand detailPageLoadedCommand,
        DetailPageSearchTextLostFocusCommand detailPageSearchTextLostFocusCommand,
        DetailPageGoBackCommand detailPageGoBackCommand,
        DetailPageOpenHomepageLinkCommand detailPageOpenHomepageLinkCommand,
        LaunchSandboxCommand launchSandboxCommand,
        CreateShortcutCommand createShortcutCommand,
        CopyCommandLineCommand copyCommandLineCommand,
        CertSelectCommand certSelectCommand)
    {
        _detailPageLoadedCommand = detailPageLoadedCommand;
        _detailPageSearchTextLostFocusCommand = detailPageSearchTextLostFocusCommand;
        _detailPageGoBackCommand = detailPageGoBackCommand;
        _detailPageOpenHomepageLinkCommand = detailPageOpenHomepageLinkCommand;
        _launchSandboxCommand = launchSandboxCommand;
        _createShortcutCommand = createShortcutCommand;
        _copyCommandLineCommand = copyCommandLineCommand;
        _certSelectCommand = certSelectCommand;
    }

    public event EventHandler? CloseRequested;

    public void RequestClose(object sender, EventArgs e)
        => CloseRequested?.Invoke(sender, e);

    private readonly DetailPageLoadedCommand _detailPageLoadedCommand;
    private readonly DetailPageSearchTextLostFocusCommand _detailPageSearchTextLostFocusCommand;
    private readonly DetailPageGoBackCommand _detailPageGoBackCommand;
    private readonly DetailPageOpenHomepageLinkCommand _detailPageOpenHomepageLinkCommand;
    private readonly LaunchSandboxCommand _launchSandboxCommand;
    private readonly CreateShortcutCommand _createShortcutCommand;
    private readonly CopyCommandLineCommand _copyCommandLineCommand;
    private readonly CertSelectCommand _certSelectCommand;

    public DetailPageLoadedCommand DetailPageLoadedCommand
        => _detailPageLoadedCommand;

    public DetailPageSearchTextLostFocusCommand DetailPageSearchTextLostFocusCommand
        => _detailPageSearchTextLostFocusCommand;

    public DetailPageGoBackCommand DetailPageGoBackCommand
        => _detailPageGoBackCommand;

    public DetailPageOpenHomepageLinkCommand DetailPageOpenHomepageLinkCommand
        => _detailPageOpenHomepageLinkCommand;

    public LaunchSandboxCommand LaunchSandboxCommand
        => _launchSandboxCommand;

    public CreateShortcutCommand CreateShortcutCommand
        => _createShortcutCommand;

    public CopyCommandLineCommand CopyCommandLineCommand
        => _copyCommandLineCommand;

    public CertSelectCommand CertSelectCommand
        => _certSelectCommand;

    private CatalogInternetService? _selectedService;

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

    private CommandLineArgumentModel? _commandLineArgumentModel;
    private ImageSource? _serviceLogo;
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
    private X509CertPair? _selectedCertFile;
    private string _searchKeyword = string.Empty;

    public CommandLineArgumentModel? CommandLineArgumentModel
    {
        get => _commandLineArgumentModel;
        set => SetProperty(ref _commandLineArgumentModel, value);
    }

    public ImageSource? ServiceLogo
    {
        get => _serviceLogo;
        set => SetProperty(ref _serviceLogo, value);
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

    public X509CertPair? SelectedCertFile
    {
        get => _selectedCertFile;
        set => SetProperty(ref _selectedCertFile, value);
    }

    public IEnumerable<CatalogInternetService> SelectedServices
        => _selectedService != null ? new CatalogInternetService[] { _selectedService, } : Enumerable.Empty<CatalogInternetService>();

    public string SearchKeyword
    {
        get => _searchKeyword;
        set => SetProperty(ref _searchKeyword, value);
    }
}
