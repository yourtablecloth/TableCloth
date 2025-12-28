using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using TableCloth.Components;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public partial class DetailPageViewModelForDesigner : DetailPageViewModel { }

public partial class DetailPageViewModel : ObservableObject
{
    protected DetailPageViewModel() { }

    public DetailPageViewModel(
        TaskFactory taskFactory,
        IResourceCacheManager resourceCacheManager,
        IPreferencesManager preferencesManager,
        IX509CertPairScanner certPairScanner,
        IAppRestartManager appRestartManager,
        IAppUserInterface appUserInterface,
        ISharedLocations sharedLocations,
        IConfigurationComposer configurationComposer,
        ISandboxLauncher sandboxLauncher,
        INavigationService navigationService,
        IShortcutCrerator shortcutCrerator,
        IAppMessageBox appMessageBox,
        ICommandLineComposer commandLineComposer)
    {
        _taskFactory = taskFactory;
        _resourceCacheManager = resourceCacheManager;
        _preferencesManager = preferencesManager;
        _certPairScanner = certPairScanner;
        _appRestartManager = appRestartManager;
        _appUserInterface = appUserInterface;
        _sharedLocations = sharedLocations;
        _configurationComposer = configurationComposer;
        _sandboxLauncher = sandboxLauncher;
        _navigationService = navigationService;
        _shortcutCrerator = shortcutCrerator;
        _appMessageBox = appMessageBox;
        _commandLineComposer = commandLineComposer;
    }

    public event EventHandler? CloseRequested;

    public async Task RequestCloseAsync(object sender, EventArgs e, CancellationToken cancellationToken = default)
        => await _taskFactory.StartNew(() => CloseRequested?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    [RelayCommand]
    private async Task DetailPageLoaded()
    {
        if (SelectedService == null)
            return;

        var services = _resourceCacheManager.CatalogDocument?.Services;
        var selectedServiceId = SelectedService.Id;
        var selectedService = services?.Where(x => string.Equals(x.Id, selectedServiceId, StringComparison.Ordinal)).FirstOrDefault();
        SelectedService = selectedService;

        var currentConfig = await _preferencesManager.LoadPreferencesAsync();
        currentConfig ??= _preferencesManager.GetDefaultPreferences();

        IsFavorite = currentConfig.Favorites.Contains(selectedServiceId);
        EnableLogAutoCollecting = currentConfig.UseLogCollection;
        EnableMicrophone = currentConfig.UseAudioRedirection;
        EnableWebCam = currentConfig.UseVideoRedirection;
        EnablePrinters = currentConfig.UsePrinterRedirection;
        InstallEveryonesPrinter = currentConfig.InstallEveryonesPrinter;
        InstallAdobeReader = currentConfig.InstallAdobeReader;
        InstallHancomOfficeViewer = currentConfig.InstallHancomOfficeViewer;
        InstallRaiDrive = currentConfig.InstallRaiDrive;
        LastDisclaimerAgreedTime = currentConfig.LastDisclaimerAgreedTime;

        var targetFilePath = _sharedLocations.GetImageFilePath(selectedServiceId);

        if (File.Exists(targetFilePath))
            ServiceLogo = _resourceCacheManager.GetImage(selectedServiceId);

        var allCerts = _certPairScanner.ScanX509Pairs(_certPairScanner.GetCandidateDirectories());
        var lastUsedCertHash = currentConfig.LastUsedCertHash;
        var selectedCert = default(X509CertPair?);

        if (!string.IsNullOrWhiteSpace(lastUsedCertHash))
            selectedCert = allCerts.FirstOrDefault(x => string.Equals(lastUsedCertHash, x.CertHash, StringComparison.Ordinal));
        else if (allCerts.Count() < 2)
            selectedCert = allCerts.Where(x => x.IsValid).FirstOrDefault();

        MapNpkiCert = (selectedCert != null);
        SelectedCertFile = selectedCert;

        PropertyChanged += ViewModel_PropertyChanged;

        if (ShouldNotifyDisclaimer)
        {
            var disclaimerWindow = _appUserInterface.CreateDisclaimerWindow();
            var result = disclaimerWindow.ShowDialog();

            if (result.HasValue && result.Value)
                LastDisclaimerAgreedTime = DateTime.UtcNow;
        }

        if (CommandLineArgumentModel != null &&
            CommandLineArgumentModel.SelectedServices.Skip(1).Any())
        {
            var config = _configurationComposer.GetConfigurationFromArgumentModel(CommandLineArgumentModel);
            await _sandboxLauncher.RunSandboxAsync(config);
        }
    }

    [RelayCommand]
    private void DetailPageGoBack()
    {
        _navigationService.GoBack();
    }

    [RelayCommand]
    private void DetailPageOpenHomepageLink()
    {
        if (!Uri.TryCreate(Url, UriKind.Absolute, out var parsedUri) || parsedUri == null)
            return;

        Process.Start(new ProcessStartInfo(parsedUri.ToString())
        {
            UseShellExecute = true,
        });
    }

    [RelayCommand]
    private void CertSelect()
    {
        var certSelectWindow = _appUserInterface.CreateCertSelectWindow(SelectedCertFile);
        var response = certSelectWindow.ShowDialog();

        if (!response.HasValue || !response.Value)
            return;

        if (certSelectWindow.ViewModel.SelectedCertPair != null)
            SelectedCertFile = certSelectWindow.ViewModel.SelectedCertPair;
    }

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

    private readonly TaskFactory _taskFactory = default!;
    private readonly IResourceCacheManager _resourceCacheManager = default!;
    private readonly IPreferencesManager _preferencesManager = default!;
    private readonly IX509CertPairScanner _certPairScanner = default!;
    private readonly IAppRestartManager _appRestartManager = default!;
    private readonly IAppUserInterface _appUserInterface = default!;
    private readonly ISharedLocations _sharedLocations = default!;
    private readonly IConfigurationComposer _configurationComposer = default!;
    private readonly ISandboxLauncher _sandboxLauncher = default!;
    private readonly INavigationService _navigationService = default!;
    private readonly IShortcutCrerator _shortcutCrerator = default!;
    private readonly IAppMessageBox _appMessageBox = default!;
    private readonly ICommandLineComposer _commandLineComposer = default!;

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => OnViewModelPropertyChangedAsync(sender, e).SafeFireAndForget();

    private async Task OnViewModelPropertyChangedAsync(object? sender, PropertyChangedEventArgs e)
    {
        var viewModel = sender as DetailPageViewModel;
        ArgumentNullException.ThrowIfNull(viewModel);

        var currentConfig = await _preferencesManager.LoadPreferencesAsync();
        currentConfig ??= _preferencesManager.GetDefaultPreferences();

        var reserveRestart = false;

        switch (e.PropertyName)
        {
            case nameof(DetailPageViewModel.IsFavorite):
                var serviceId = viewModel.SelectedService?.Id;
                if (!string.IsNullOrWhiteSpace(serviceId))
                    if (!currentConfig.Favorites.Contains(serviceId))
                        currentConfig.Favorites.Add(serviceId);
                break;

            case nameof(DetailPageViewModel.EnableLogAutoCollecting):
                currentConfig.UseLogCollection = viewModel.EnableLogAutoCollecting;
                reserveRestart = _appRestartManager.AskRestart();
                break;

            case nameof(DetailPageViewModel.EnableMicrophone):
                currentConfig.UseAudioRedirection = viewModel.EnableMicrophone;
                break;

            case nameof(DetailPageViewModel.EnableWebCam):
                currentConfig.UseVideoRedirection = viewModel.EnableWebCam;
                break;

            case nameof(DetailPageViewModel.EnablePrinters):
                currentConfig.UsePrinterRedirection = viewModel.EnablePrinters;
                break;

            case nameof(DetailPageViewModel.InstallEveryonesPrinter):
                currentConfig.InstallEveryonesPrinter = viewModel.InstallEveryonesPrinter;
                break;

            case nameof(DetailPageViewModel.InstallAdobeReader):
                currentConfig.InstallAdobeReader = viewModel.InstallAdobeReader;
                break;

            case nameof(DetailPageViewModel.InstallHancomOfficeViewer):
                currentConfig.InstallHancomOfficeViewer = viewModel.InstallHancomOfficeViewer;
                break;

            case nameof(DetailPageViewModel.InstallRaiDrive):
                currentConfig.InstallRaiDrive = viewModel.InstallRaiDrive;
                break;

            case nameof(DetailPageViewModel.LastDisclaimerAgreedTime):
                currentConfig.LastDisclaimerAgreedTime = viewModel.LastDisclaimerAgreedTime;
                break;

            case nameof(DetailPageViewModel.SelectedCertFile):
                currentConfig.LastUsedCertHash = viewModel.SelectedCertFile?.CertHash;
                break;

            default:
                return;
        }

        await _preferencesManager.SavePreferencesAsync(currentConfig);

        if (reserveRestart)
        {
            _appRestartManager.ReserveRestart();
            await viewModel.RequestCloseAsync(viewModel, e);
        }
    }

    [RelayCommand]
    private void DetailPageSearchTextLostFocus()
    {
        _navigationService.NavigateToCatalog(SearchKeyword);
    }

    [RelayCommand]
    private async Task CreateShortcut()
    {
        if (!SelectedServices.Any())
        {
            _appMessageBox.DisplayError("No site selected for shortcut creation.", false);
            return;
        }

        if (SelectedServices.Count() > 1)
            _appMessageBox.DisplayInfo("Will create single site shortcut.");

        await _shortcutCrerator.CreateShortcutAsync(this);
        //await _shortcutCrerator.CreateResponseFileAsync(this);
    }

    [RelayCommand]
    private async Task LaunchSandbox()
    {
        var config = _configurationComposer.GetConfigurationFromViewModel(this);
        await _sandboxLauncher.RunSandboxAsync(config);
    }

    [RelayCommand]
    private void CopyCommandLine()
    {
        var expression = _commandLineComposer.ComposeCommandLineExpression(this, true);

        try { Clipboard.SetText(expression); }
        catch (Exception thrownException)
        {
            _appMessageBox.DisplayError(
                $"Cannot copy to clipboard: {thrownException.Message}",
                false);
        }

        _appMessageBox.DisplayInfo("Command line copied to clipboard.", MessageBoxButton.OK);
    }

    [RelayCommand]
    private void ShowDebugInfo()
    {
        var debugInfo = $"Selected Service: {DisplayName} ({Id})\nURL: {Url}\nCompatibility Notes: {CompatibilityNotes}\nIs Favorite: {IsFavorite}";
        _appMessageBox.DisplayInfo(debugInfo, MessageBoxButton.OK);
    }

    [RelayCommand]
    private async Task DetailPageItemFavorite()
    {
        var settings = await _preferencesManager.LoadPreferencesAsync();
        var currentId = Id;

        if (!string.IsNullOrWhiteSpace(currentId))
        {
            settings!.Favorites ??= new List<string>();
            if (IsFavorite)
                settings.Favorites.Add(currentId);
            else if (settings.Favorites.Contains(currentId))
                settings.Favorites.Remove(currentId);

            await _preferencesManager.SavePreferencesAsync(settings);
        }
    }
}
