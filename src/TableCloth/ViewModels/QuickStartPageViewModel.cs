using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TableCloth.Components;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Resources;

namespace TableCloth.ViewModels;

[Obsolete("This class is reserved for design-time usage.", false)]
public partial class QuickStartPageViewModelForDesigner : QuickStartPageViewModel { }

public partial class QuickStartPageViewModel : ObservableObject
{
    protected QuickStartPageViewModel() { }

    [ActivatorUtilitiesConstructor]
    public QuickStartPageViewModel(
        IPreferencesManager preferencesManager,
        IX509CertPairScanner certPairScanner,
        IAppRestartManager appRestartManager,
        IAppUserInterface appUserInterface,
        ISharedLocations sharedLocations,
        ISandboxLauncher sandboxLauncher,
        IAppMessageBox appMessageBox,
        TaskFactory taskFactory)
    {
        _preferencesManager = preferencesManager;
        _certPairScanner = certPairScanner;
        _appRestartManager = appRestartManager;
        _appUserInterface = appUserInterface;
        _sharedLocations = sharedLocations;
        _sandboxLauncher = sandboxLauncher;
        _appMessageBox = appMessageBox;
        _taskFactory = taskFactory;
    }

    public event EventHandler? CloseRequested;

    public async Task RequestCloseAsync(object sender, EventArgs e, CancellationToken cancellationToken = default)
        => await _taskFactory.StartNew(() => CloseRequested?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    [RelayCommand]
    private async Task QuickStartPageLoaded()
    {
        var currentConfig = await _preferencesManager.LoadPreferencesAsync();
        currentConfig ??= _preferencesManager.GetDefaultPreferences();

        EnableLogAutoCollecting = currentConfig.UseLogCollection;
        EnableMicrophone = currentConfig.UseAudioRedirection;
        EnableWebCam = currentConfig.UseVideoRedirection;
        EnablePrinters = currentConfig.UsePrinterRedirection;
        InstallEveryonesPrinter = currentConfig.InstallEveryonesPrinter;
        InstallAdobeReader = currentConfig.InstallAdobeReader;
        InstallHancomOfficeViewer = currentConfig.InstallHancomOfficeViewer;
        InstallRaiDrive = currentConfig.InstallRaiDrive;
        LastDisclaimerAgreedTime = currentConfig.LastDisclaimerAgreedTime;

        DataDirectoryHostPath = _sharedLocations.GetEffectiveDataDirectoryPath(currentConfig.DataDirectoryHostPath);

        MappedFolders.Clear();
        foreach (var folder in currentConfig.MappedFolders ?? new List<MappedFolderSetting>())
            MappedFolders.Add(folder);

        var lastUsedCertHash = currentConfig.LastUsedCertHash;
        var allCerts = await Task.Run(() =>
            _certPairScanner.ScanX509Pairs(_certPairScanner.GetCandidateDirectories()).ToList());

        var selectedCert = default(X509CertPair?);

        if (!string.IsNullOrWhiteSpace(lastUsedCertHash))
            selectedCert = allCerts.FirstOrDefault(x => string.Equals(lastUsedCertHash, x.CertHash, StringComparison.Ordinal));
        else if (allCerts.Count < 2)
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

    [RelayCommand]
    private async Task BrowseDataDirectory()
    {
        var selectedPath = ShowFolderBrowserDialog(UIStringResources.QuickStart_DataDirectory_SelectFolderTitle, DataDirectoryHostPath);

        if (string.IsNullOrWhiteSpace(selectedPath))
            return;

        DataDirectoryHostPath = selectedPath;
        await SaveDataDirectoryAsync();
    }

    [RelayCommand]
    private async Task ResetDataDirectoryToDefault()
    {
        DataDirectoryHostPath = _sharedLocations.DefaultDataDirectoryPath;
        await SaveDataDirectoryAsync(useDefault: true);
    }

    [RelayCommand]
    private void OpenDataDirectory()
    {
        if (string.IsNullOrWhiteSpace(DataDirectoryHostPath))
            return;

        try
        {
            if (!Directory.Exists(DataDirectoryHostPath))
                Directory.CreateDirectory(DataDirectoryHostPath);

            Process.Start(new ProcessStartInfo
            {
                FileName = DataDirectoryHostPath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _appMessageBox.DisplayError(ex, false);
        }
    }

    [RelayCommand]
    private async Task AddMappedFolder()
    {
        var selectedPath = ShowFolderBrowserDialog(UIStringResources.MappedFolder_SelectFolder, null);

        if (string.IsNullOrWhiteSpace(selectedPath))
            return;

        if (MappedFolders.Any(f => string.Equals(f.HostFolder, selectedPath, StringComparison.OrdinalIgnoreCase)))
        {
            _appMessageBox.DisplayError(UIStringResources.MappedFolder_AlreadyExists, false);
            return;
        }

        MappedFolders.Add(new MappedFolderSetting
        {
            HostFolder = selectedPath,
            ReadOnly = true,
        });

        await SaveMappedFoldersAsync();
    }

    [RelayCommand]
    private async Task RemoveMappedFolder()
    {
        if (SelectedMappedFolder == null)
            return;

        MappedFolders.Remove(SelectedMappedFolder);
        SelectedMappedFolder = null;
        await SaveMappedFoldersAsync();
    }

    [RelayCommand]
    private async Task ToggleMappedFolderReadOnly()
    {
        if (SelectedMappedFolder == null)
            return;

        SelectedMappedFolder.ReadOnly = !SelectedMappedFolder.ReadOnly;
        await SaveMappedFoldersAsync();

        var index = MappedFolders.IndexOf(SelectedMappedFolder);
        if (index >= 0)
        {
            var item = SelectedMappedFolder;
            MappedFolders.RemoveAt(index);
            MappedFolders.Insert(index, item);
            SelectedMappedFolder = item;
        }
    }

    [RelayCommand]
    private async Task LaunchSandbox()
    {
        if (!Directory.Exists(DataDirectoryHostPath))
        {
            try { Directory.CreateDirectory(DataDirectoryHostPath); }
            catch (Exception ex)
            {
                _appMessageBox.DisplayError(ex, true);
                return;
            }
        }

        var selectedCert = MapNpkiCert ? SelectedCertFile : null;

        var config = new TableClothConfiguration
        {
            CertPair = selectedCert,
            EnableMicrophone = EnableMicrophone,
            EnableWebCam = EnableWebCam,
            EnablePrinters = EnablePrinters,
            InstallEveryonesPrinter = InstallEveryonesPrinter,
            InstallAdobeReader = InstallAdobeReader,
            InstallHancomOfficeViewer = InstallHancomOfficeViewer,
            InstallRaiDrive = InstallRaiDrive,
            Companions = Array.Empty<CatalogCompanion>(),
            Services = Array.Empty<CatalogInternetService>(),
            MappedFolders = MappedFolders.ToList(),
        };

        await _sandboxLauncher.RunSandboxAsync(config);
    }

    [RelayCommand]
    private void ShowDebugInfo()
    {
        var debugInfo =
            $"Data: {DataDirectoryHostPath}\n" +
            $"Cert: {(MapNpkiCert ? SelectedCertFile?.SubjectNameForNpkiApp ?? "(none)" : "off")}\n" +
            $"User folders: {MappedFolders.Count}";
        _appMessageBox.DisplayInfo(debugInfo, MessageBoxButton.OK);
    }

    [RelayCommand]
    private void AboutThisApp()
    {
        var aboutWindow = _appUserInterface.CreateAboutWindow();
        aboutWindow.ShowDialog();
    }

    [RelayCommand]
    private void ReportSite()
    {
        var siteReportWindow = _appUserInterface.CreateSiteReportWindow();
        siteReportWindow.ShowDialog();
    }

    private static string? ShowFolderBrowserDialog(string title, string? initialDirectory)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false,
        };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;

        if (dialog.ShowDialog() == true)
            return dialog.FolderName;

        return null;
    }

    private async Task SaveDataDirectoryAsync(bool useDefault = false)
    {
        var currentConfig = await _preferencesManager.LoadPreferencesAsync();
        currentConfig ??= _preferencesManager.GetDefaultPreferences();

        currentConfig.DataDirectoryHostPath = useDefault ? null : DataDirectoryHostPath;
        await _preferencesManager.SavePreferencesAsync(currentConfig);
    }

    private async Task SaveMappedFoldersAsync()
    {
        var currentConfig = await _preferencesManager.LoadPreferencesAsync();
        currentConfig ??= _preferencesManager.GetDefaultPreferences();

        currentConfig.MappedFolders = MappedFolders.ToList();
        await _preferencesManager.SavePreferencesAsync(currentConfig);
    }

    [ObservableProperty]
    private string _dataDirectoryHostPath = string.Empty;

    [ObservableProperty]
    private bool _mapNpkiCert;

    [ObservableProperty]
    private X509CertPair? _selectedCertFile;

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
    private ObservableCollection<MappedFolderSetting> _mappedFolders = new();

    [ObservableProperty]
    private MappedFolderSetting? _selectedMappedFolder;

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

    private readonly IPreferencesManager _preferencesManager = default!;
    private readonly IX509CertPairScanner _certPairScanner = default!;
    private readonly IAppRestartManager _appRestartManager = default!;
    private readonly IAppUserInterface _appUserInterface = default!;
    private readonly ISharedLocations _sharedLocations = default!;
    private readonly ISandboxLauncher _sandboxLauncher = default!;
    private readonly IAppMessageBox _appMessageBox = default!;
    private readonly TaskFactory _taskFactory = default!;

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => OnViewModelPropertyChangedAsync(sender, e).SafeFireAndForget();

    private async Task OnViewModelPropertyChangedAsync(object? sender, PropertyChangedEventArgs e)
    {
        var viewModel = sender as QuickStartPageViewModel;
        ArgumentNullException.ThrowIfNull(viewModel);

        var currentConfig = await _preferencesManager.LoadPreferencesAsync();
        currentConfig ??= _preferencesManager.GetDefaultPreferences();

        var reserveRestart = false;

        switch (e.PropertyName)
        {
            case nameof(EnableLogAutoCollecting):
                currentConfig.UseLogCollection = viewModel.EnableLogAutoCollecting;
                reserveRestart = _appRestartManager.AskRestart();
                break;

            case nameof(EnableMicrophone):
                currentConfig.UseAudioRedirection = viewModel.EnableMicrophone;
                break;

            case nameof(EnableWebCam):
                currentConfig.UseVideoRedirection = viewModel.EnableWebCam;
                break;

            case nameof(EnablePrinters):
                currentConfig.UsePrinterRedirection = viewModel.EnablePrinters;
                break;

            case nameof(InstallEveryonesPrinter):
                currentConfig.InstallEveryonesPrinter = viewModel.InstallEveryonesPrinter;
                break;

            case nameof(InstallAdobeReader):
                currentConfig.InstallAdobeReader = viewModel.InstallAdobeReader;
                break;

            case nameof(InstallHancomOfficeViewer):
                currentConfig.InstallHancomOfficeViewer = viewModel.InstallHancomOfficeViewer;
                break;

            case nameof(InstallRaiDrive):
                currentConfig.InstallRaiDrive = viewModel.InstallRaiDrive;
                break;

            case nameof(LastDisclaimerAgreedTime):
                currentConfig.LastDisclaimerAgreedTime = viewModel.LastDisclaimerAgreedTime;
                break;

            case nameof(SelectedCertFile):
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
}
