using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TableCloth.Components;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Models.WindowsSandbox;
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
        IAppUserInterface appUserInterface,
        ISharedLocations sharedLocations,
        ISandboxLauncher sandboxLauncher,
        IAppMessageBox appMessageBox,
        IMessageBoxService messageBoxService,
        IApplicationService applicationService,
        TaskFactory taskFactory)
    {
        _preferencesManager = preferencesManager;
        _appUserInterface = appUserInterface;
        _sharedLocations = sharedLocations;
        _sandboxLauncher = sandboxLauncher;
        _appMessageBox = appMessageBox;
        _messageBoxService = messageBoxService;
        _applicationService = applicationService;
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

        _dataDirectoryHostPath = _sharedLocations.GetEffectiveDataDirectoryPath(currentConfig.DataDirectoryHostPath);

        MappedFolders.Clear();
        foreach (var folder in currentConfig.MappedFolders ?? new List<MappedFolderSetting>())
            MappedFolders.Add(folder);

        LastDisclaimerAgreedTime = currentConfig.LastDisclaimerAgreedTime;

        if (ShouldNotifyDisclaimer)
        {
            var disclaimerWindow = _appUserInterface.CreateDisclaimerWindow();
            var result = disclaimerWindow.ShowDialog();

            if (result.HasValue && result.Value)
            {
                LastDisclaimerAgreedTime = DateTime.UtcNow;
                currentConfig.LastDisclaimerAgreedTime = LastDisclaimerAgreedTime;
                await _preferencesManager.SavePreferencesAsync(currentConfig);
            }
        }
    }

    [RelayCommand]
    private void OpenOptions()
    {
        var optionsWindow = _appUserInterface.CreateOptionsWindow();
        optionsWindow.ShowDialog();
    }

    [RelayCommand]
    private async Task AddMappedFolder()
    {
        var selectedPath = ShowFolderBrowserDialog(UIStringResources.MappedFolder_SelectFolder);

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
        if (!await EnsureDataDirectoryAsync())
            return;

        var currentConfig = await _preferencesManager.LoadPreferencesAsync();
        currentConfig ??= _preferencesManager.GetDefaultPreferences();

        var mappedFolders = MappedFolders.ToList();

        // Data 디렉터리는 항상 RW로 마운트. 구 버전 Windows Sandbox 호환을 위해
        // SandboxFolder는 지정하지 않으며, 호스트 폴더의 leaf 이름(기본값: "Data")이
        // 그대로 샌드박스 데스크톱(SandboxMountPaths.DataDirectory)에 노출된다.
        mappedFolders.Insert(0, new MappedFolderSetting
        {
            HostFolder = _dataDirectoryHostPath,
            SandboxFolder = null,
            ReadOnly = false,
        });

        // NPKI 폴더가 존재하면 자동으로 RO 마운트. 마찬가지로 SandboxFolder는 지정하지 않고
        // 데스크톱의 "NPKI" 폴더로 노출되며, startup 스크립트가 이를
        // SandboxMountPaths.NpkiCanonicalPath로 junction 링크하여 은행 소프트웨어가 인식하도록 한다.
        var npkiHostPath = TryGetHostNpkiDirectoryPath();
        if (npkiHostPath != null)
        {
            mappedFolders.Insert(1, new MappedFolderSetting
            {
                HostFolder = npkiHostPath,
                SandboxFolder = null,
                ReadOnly = true,
            });
        }

        var config = new TableClothConfiguration
        {
            CertPair = null,
            EnableMicrophone = currentConfig.UseAudioRedirection,
            EnableWebCam = currentConfig.UseVideoRedirection,
            EnablePrinters = currentConfig.UsePrinterRedirection,
            InstallEveryonesPrinter = currentConfig.InstallEveryonesPrinter,
            InstallAdobeReader = currentConfig.InstallAdobeReader,
            InstallHancomOfficeViewer = currentConfig.InstallHancomOfficeViewer,
            InstallRaiDrive = currentConfig.InstallRaiDrive,
            Companions = Array.Empty<CatalogCompanion>(),
            Services = Array.Empty<CatalogInternetService>(),
            MappedFolders = mappedFolders,
        };

        await _sandboxLauncher.RunSandboxAsync(config);
    }

    [RelayCommand]
    private void ShowDebugInfo()
    {
        var npki = TryGetHostNpkiDirectoryPath() ?? "(not found)";
        var debugInfo =
            $"Data: {_dataDirectoryHostPath}\n" +
            $"NPKI: {npki}\n" +
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

    private async Task<bool> EnsureDataDirectoryAsync()
    {
        if (Directory.Exists(_dataDirectoryHostPath))
            return true;

        var prompt = string.Format(UIStringResources.QuickStart_DataDirectory_CreatePrompt, _dataDirectoryHostPath);
        var result = _messageBoxService.Show(
            _applicationService.GetActiveWindow() ?? _applicationService.GetMainWindow(),
            prompt,
            UIStringResources.QuickStart_Title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.Yes);

        if (result != MessageBoxResult.Yes)
            return false;

        try
        {
            await Task.Run(() => Directory.CreateDirectory(_dataDirectoryHostPath));
            return true;
        }
        catch (Exception ex)
        {
            _appMessageBox.DisplayError(ex, true);
            _appMessageBox.DisplayError(UIStringResources.QuickStart_DataDirectory_CreateFailed, false);
            return false;
        }
    }

    private static string? TryGetHostNpkiDirectoryPath()
    {
        var localLowPath = NativeMethods.GetKnownFolderPath(NativeMethods.LocalLowFolderGuid);

        if (string.IsNullOrWhiteSpace(localLowPath))
            return null;

        var candidate = Path.Combine(localLowPath, "NPKI");
        return Directory.Exists(candidate) ? candidate : null;
    }

    private static string? ShowFolderBrowserDialog(string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false,
        };

        if (dialog.ShowDialog() == true)
            return dialog.FolderName;

        return null;
    }

    private async Task SaveMappedFoldersAsync()
    {
        var currentConfig = await _preferencesManager.LoadPreferencesAsync();
        currentConfig ??= _preferencesManager.GetDefaultPreferences();

        currentConfig.MappedFolders = MappedFolders.ToList();
        await _preferencesManager.SavePreferencesAsync(currentConfig);
    }

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

    private string _dataDirectoryHostPath = string.Empty;

    private readonly IPreferencesManager _preferencesManager = default!;
    private readonly IAppUserInterface _appUserInterface = default!;
    private readonly ISharedLocations _sharedLocations = default!;
    private readonly ISandboxLauncher _sandboxLauncher = default!;
    private readonly IAppMessageBox _appMessageBox = default!;
    private readonly IMessageBoxService _messageBoxService = default!;
    private readonly IApplicationService _applicationService = default!;
    private readonly TaskFactory _taskFactory = default!;
}
