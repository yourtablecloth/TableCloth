﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using TableCloth.Commands;
using TableCloth.Commands.DetailPage;
using TableCloth.Commands.Shared;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public class DetailPageViewModelForDesigner : DetailPageViewModel { }

public class DetailPageViewModel : ViewModelBase, ITableClothViewModel
{
    protected DetailPageViewModel() { }

    public DetailPageViewModel(
        DetailPageLoadedCommand detailPageLoadedCommand,
        DetailPageSearchTextLostFocusCommand detailPageSearchTextLostFocusCommand,
        DetailPageGoBackCommand detailPageGoBackCommand,
        DetailPageOpenHomepageLinkCommand detailPageOpenHomepageLinkCommand,
        DetailPageItemFavoriteCommand detailPageItemFavoriteCommand,
        LaunchSandboxCommand launchSandboxCommand,
        CreateShortcutCommand createShortcutCommand,
        CopyCommandLineCommand copyCommandLineCommand,
        CertSelectCommand certSelectCommand,
        ShowDebugInfoCommand showDebugInfoCommand)
    {
        _detailPageLoadedCommand = detailPageLoadedCommand;
        _detailPageSearchTextLostFocusCommand = detailPageSearchTextLostFocusCommand;
        _detailPageGoBackCommand = detailPageGoBackCommand;
        _detailPageOpenHomepageLinkCommand = detailPageOpenHomepageLinkCommand;
        _detailPageItemFavoriteCommand = detailPageItemFavoriteCommand;
        _launchSandboxCommand = launchSandboxCommand;
        _createShortcutCommand = createShortcutCommand;
        _copyCommandLineCommand = copyCommandLineCommand;
        _certSelectCommand = certSelectCommand;
        _showDebugInfoCommand = showDebugInfoCommand;
    }

    public event EventHandler? CloseRequested;

    public async Task RequestCloseAsync(object sender, EventArgs e, CancellationToken cancellationToken = default)
        => await TaskFactory.StartNew(() => CloseRequested?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    private readonly DetailPageLoadedCommand _detailPageLoadedCommand = default!;
    private readonly DetailPageSearchTextLostFocusCommand _detailPageSearchTextLostFocusCommand = default!;
    private readonly DetailPageGoBackCommand _detailPageGoBackCommand = default!;
    private readonly DetailPageOpenHomepageLinkCommand _detailPageOpenHomepageLinkCommand = default!;
    private readonly DetailPageItemFavoriteCommand _detailPageItemFavoriteCommand = default!;
    private readonly LaunchSandboxCommand _launchSandboxCommand = default!;
    private readonly CreateShortcutCommand _createShortcutCommand = default!;
    private readonly CopyCommandLineCommand _copyCommandLineCommand = default!;
    private readonly CertSelectCommand _certSelectCommand = default!;
    private readonly ShowDebugInfoCommand _showDebugInfoCommand = default!;

    public DetailPageLoadedCommand DetailPageLoadedCommand
        => _detailPageLoadedCommand;

    public DetailPageSearchTextLostFocusCommand DetailPageSearchTextLostFocusCommand
        => _detailPageSearchTextLostFocusCommand;

    public DetailPageGoBackCommand DetailPageGoBackCommand
        => _detailPageGoBackCommand;

    public DetailPageOpenHomepageLinkCommand DetailPageOpenHomepageLinkCommand
        => _detailPageOpenHomepageLinkCommand;

    public DetailPageItemFavoriteCommand DetailPageItemFavoriteCommand
        => _detailPageItemFavoriteCommand;

    public LaunchSandboxCommand LaunchSandboxCommand
        => _launchSandboxCommand;

    public CreateShortcutCommand CreateShortcutCommand
        => _createShortcutCommand;

    public CopyCommandLineCommand CopyCommandLineCommand
        => _copyCommandLineCommand;

    public CertSelectCommand CertSelectCommand
        => _certSelectCommand;

    public ShowDebugInfoCommand ShowDebugInfoCommand
        => _showDebugInfoCommand;

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

    public string? MatchedKeywords
        => string.Join(", ", _selectedService?.GetSearchKeywords() ?? new string[] { });

    public bool ShowMatchedKeywordsRow
        => !string.IsNullOrWhiteSpace((MatchedKeywords ?? string.Empty).Trim());

    public bool ShowCompatibilityNotesRow
        => !string.IsNullOrWhiteSpace((CompatibilityNotes ?? string.Empty).Trim());

    private CommandLineArgumentModel? _commandLineArgumentModel;
    private bool _isFavorite;
    private ImageSource? _serviceLogo;
    private bool _mapNpkiCert;
    private bool _enableLogAutoCollecting;
    private bool _enableMicrophone;
    private bool _enableWebCam;
    private bool _enablePrinters;
    private bool _installEveryonesPrinter;
    private bool _installAdobeReader;
    private bool _installHancomOfficeViewer;
    private bool _installRaiDrive;
    private DateTime? _lastDisclaimerAgreedTime;
    private X509CertPair? _selectedCertFile;
    private string _searchKeyword = string.Empty;

    public CommandLineArgumentModel? CommandLineArgumentModel
    {
        get => _commandLineArgumentModel;
        set => SetProperty(ref _commandLineArgumentModel, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetProperty(ref _isFavorite, value);
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
