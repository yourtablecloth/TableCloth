using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TableCloth.Components;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Models.UserData;
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
        // 옵션 창은 자체적으로 환경 설정을 읽고 매핑 폴더 목록을 표시·편집한다.
        // QuickStart는 launch 시 환경 설정에서 다시 읽으므로 별도 동기화가 필요 없다.
        var optionsWindow = _appUserInterface.CreateOptionsWindow();
        optionsWindow.ShowDialog();
    }

    [RelayCommand]
    private async Task LaunchSandbox()
    {
        if (!await EnsureDataDirectoryAsync())
            return;

        var currentConfig = await _preferencesManager.LoadPreferencesAsync();
        currentConfig ??= _preferencesManager.GetDefaultPreferences();

        await MigrateUserDataIfNeededAsync(currentConfig);

        // 사용자 추가 폴더의 가용성을 매 시작 시점에 검증한다. 존재/접근 불가한 항목은
        // 메시지 박스 없이 마운트 대상에서 조용히 제외한다. 사용자에게는 옵션 창의
        // 사용자 폴더 탭에서 "사용 불가" 배지로 별도 노출된다.
        var userFolders = (currentConfig.MappedFolders ?? new List<MappedFolderSetting>())
            .Where(f => FolderIsAccessible(f.HostFolder))
            .ToList();

        var mappedFolders = new List<MappedFolderSetting>(userFolders);

        // Data 디렉터리는 항상 RW로 마운트. 구 버전 Windows Sandbox 호환을 위해
        // SandboxFolder는 지정하지 않으며, 호스트 폴더의 leaf 이름(기본값: "Data")이
        // 그대로 샌드박스 데스크톱(SandboxMountPaths.DataDirectory)에 노출된다.
        mappedFolders.Insert(0, new MappedFolderSetting
        {
            HostFolder = _dataDirectoryHostPath,
            SandboxFolder = null,
            ReadOnly = false,
        });

        // NPKI 폴더가 존재하면 자동으로 RO 마운트. SandboxFolder는 지정하지 않고
        // 데스크톱의 "NPKI" 폴더로 노출되며, startup 스크립트가 이를
        // SandboxMountPaths.NpkiCanonicalPath로 xcopy 복사하여 은행 소프트웨어가 인식하도록 한다.
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
            $"NPKI: {npki}";
        _appMessageBox.DisplayInfo(debugInfo, MessageBoxButton.OK);
    }

    [RelayCommand]
    private void AboutThisApp()
    {
        var aboutWindow = _appUserInterface.CreateAboutWindow();
        aboutWindow.ShowDialog();
    }

    /// <summary>
    /// 직전 버전(호스트 측 <see cref="PreferenceSettings.Favorites"/>)에 보관되어 있던
    /// 즐겨찾기 목록을 Data 디렉터리의 user-data.json으로 1회성 이전한다.
    /// 이미 Data 디렉터리에 user-data.json이 존재하면 source-of-truth가 그쪽이므로 건드리지 않는다.
    /// </summary>
    private async Task MigrateUserDataIfNeededAsync(PreferenceSettings currentConfig)
    {
        try
        {
            var userDataPath = Path.Combine(_dataDirectoryHostPath, SporkUserData.FileName);

            if (File.Exists(userDataPath))
                return;

            var legacyFavorites = currentConfig.Favorites ?? new List<string>();
            if (legacyFavorites.Count == 0)
                return;

            var userData = new SporkUserData
            {
                Favorites = legacyFavorites.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                ShowFavoritesOnly = currentConfig.ShowFavoritesOnly,
            };

            using (var stream = File.Create(userDataPath))
            {
                await JsonSerializer.SerializeAsync(stream, userData, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch (Exception ex)
        {
            // 마이그레이션 실패가 샌드박스 실행을 막아서는 안 된다. 사용자는 샌드박스 안에서
            // 즐겨찾기를 다시 등록할 수 있다.
            _appMessageBox.DisplayError(ex, false);
        }
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

    private static bool FolderIsAccessible(string path)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
        }
        catch
        {
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

    [ObservableProperty]
    private DateTime? _lastDisclaimerAgreedTime;

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
