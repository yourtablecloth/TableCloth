using CommunityToolkit.Mvvm.ComponentModel;
using System;
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
public partial class DetailPageViewModelForDesigner : DetailPageViewModel { }

public partial class DetailPageViewModel : ViewModelBase, ITableClothViewModel
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

    [ObservableProperty]
    private DetailPageLoadedCommand _detailPageLoadedCommand = default!;

    [ObservableProperty]
    private DetailPageSearchTextLostFocusCommand _detailPageSearchTextLostFocusCommand = default!;

    [ObservableProperty]
    private DetailPageGoBackCommand _detailPageGoBackCommand = default!;

    [ObservableProperty]
    private DetailPageOpenHomepageLinkCommand _detailPageOpenHomepageLinkCommand = default!;

    [ObservableProperty]
    private DetailPageItemFavoriteCommand _detailPageItemFavoriteCommand = default!;

    [ObservableProperty]
    private LaunchSandboxCommand _launchSandboxCommand = default!;

    [ObservableProperty]
    private CreateShortcutCommand _createShortcutCommand = default!;

    [ObservableProperty]
    private CopyCommandLineCommand _copyCommandLineCommand = default!;

    [ObservableProperty]
    private CertSelectCommand _certSelectCommand = default!;

    [ObservableProperty]
    private ShowDebugInfoCommand _showDebugInfoCommand = default!;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Id))]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(Url))]
    [NotifyPropertyChangedFor(nameof(CompatibilityNotes))]
    [NotifyPropertyChangedFor(nameof(PackageCountForDisplay))]
    [NotifyPropertyChangedFor(nameof(ServiceLogo))]
    private CatalogInternetService? _selectedService;

    public string? Id
        => SelectedService?.Id;

    public string? DisplayName
        => SelectedService?.DisplayName;

    public string? Url
        => SelectedService?.Url;

    public string? CompatibilityNotes
        => SelectedService?.CompatibilityNotes;

    public int? PackageCountForDisplay
        => SelectedService?.PackageCountForDisplay;

    public string? MatchedKeywords
        => string.Join(", ", SelectedService?.GetSearchKeywords() ?? new string[] { });

    public bool ShowMatchedKeywordsRow
        => !string.IsNullOrWhiteSpace((MatchedKeywords ?? string.Empty).Trim());

    public bool ShowCompatibilityNotesRow
        => !string.IsNullOrWhiteSpace((CompatibilityNotes ?? string.Empty).Trim());

    [ObservableProperty]
    private CommandLineArgumentModel? _commandLineArgumentModel;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private ImageSource? _serviceLogo;

    [ObservableProperty]
    private bool _mapNpkiCert;

    [ObservableProperty]
    private bool _enableLogAutoCollecting;

    [ObservableProperty]
    private bool _enableMicrophone;

    [ObservableProperty]
    private bool _enableWebCam;

    [ObservableProperty]
    private bool _enablePrinters;

    [ObservableProperty]
    private bool _installEveryonesPrinter;

    [ObservableProperty]
    private bool _installAdobeReader;

    [ObservableProperty]
    private bool _installHancomOfficeViewer;

    [ObservableProperty]
    private bool _installRaiDrive;

    [ObservableProperty]
    private DateTime? _lastDisclaimerAgreedTime;

    [ObservableProperty]
    private X509CertPair? _selectedCertFile;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    public bool ShouldNotifyDisclaimer
    {
        get
        {
            if (!LastDisclaimerAgreedTime.HasValue)
                return true;

            if ((DateTime.UtcNow - LastDisclaimerAgreedTime.Value).TotalDays >= 7d)
                return true;

            return false;
        }
    }

    public IEnumerable<CatalogInternetService> SelectedServices
        => SelectedService != null ? new CatalogInternetService[] { SelectedService, } : Enumerable.Empty<CatalogInternetService>();
}
