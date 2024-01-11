using System;
using System.Collections.Generic;
using TableCloth.Commands;
using TableCloth.Commands.MainWindow;
using TableCloth.Commands.Shared;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Resources;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public class MainWindowViewModelForDesigner : MainWindowViewModel
{
    public IList<CatalogInternetService> ServicesForDesigner
        => DesignTimeCatalog.DesignTimeCatalogDocument.Services;
}

public class MainWindowViewModel : ViewModelBase, ITableClothViewModel
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    protected MainWindowViewModel() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public MainWindowViewModel(
        MainWindowLoadedCommand mainWindowLoadedCommand,
        MainWindowClosedCommand mainWindowClosedCommand,
        LaunchSandboxCommand launchSandboxCommand,
        CreateShortcutCommand createShortcutCommand,
        AppRestartCommand appRestartCommand,
        AboutThisAppCommand aboutThisAppCommand,
        ShowDebugInfoCommand showDebugInfoCommand,
        CertSelectCommand certSelectCommand)
    {
        _mainWindowLoadedCommand = mainWindowLoadedCommand;
        _mainWindowClosedCommand = mainWindowClosedCommand;
        _launchSandboxCommand = launchSandboxCommand;
        _createShortcutCommand = createShortcutCommand;
        _appRestartCommand = appRestartCommand;
        _aboutThisAppCommand = aboutThisAppCommand;
        _showDebugInfoCommand = showDebugInfoCommand;
        _certSelectCommand = certSelectCommand;
    }

    private readonly MainWindowLoadedCommand _mainWindowLoadedCommand;
    private readonly MainWindowClosedCommand _mainWindowClosedCommand;
    private readonly LaunchSandboxCommand _launchSandboxCommand;
    private readonly CreateShortcutCommand _createShortcutCommand;
    private readonly AppRestartCommand _appRestartCommand;
    private readonly AboutThisAppCommand _aboutThisAppCommand;
    private readonly ShowDebugInfoCommand _showDebugInfoCommand;
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
    private X509CertPair? _selectedCertFile;
    private IList<CatalogInternetService> _services = new List<CatalogInternetService>();
    private IList<CatalogInternetService> _selectedServices = new List<CatalogInternetService>();

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

    public ShowDebugInfoCommand ShowDebugInfoCommand
        => _showDebugInfoCommand;

    public CertSelectCommand CertSelectCommand
        => _certSelectCommand;

    public bool DebugMode
        => Helpers.IsDevelopmentBuild;

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

    public X509CertPair? SelectedCertFile
    {
        get => _selectedCertFile;
        set => SetProperty(ref _selectedCertFile, value);
    }

    public IList<CatalogInternetService> SelectedServices
    {
        get => _selectedServices;
        set => SetProperty(ref _selectedServices, value);
    }

    IEnumerable<CatalogInternetService> ITableClothViewModel.SelectedServices
        => this.SelectedServices;

    public IList<CatalogInternetService> Services
    {
        get => _services;
        set => SetProperty(ref _services, value, new string[] { nameof(Services), nameof(HasServices), });
    }

    public bool HasServices
        => _services.Count > 0;
}
