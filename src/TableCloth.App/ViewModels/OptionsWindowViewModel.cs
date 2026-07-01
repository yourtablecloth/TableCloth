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
using TableCloth.Models.Configuration;
using TableCloth.Resources;

namespace TableCloth.ViewModels;

/// <summary>
/// 옵션 창의 탭을 호출 측이 식별하기 위한 키 상수. 문자열을 그대로 비교하므로
/// 새 탭 추가 시 키를 새로 정의하고 <see cref="OptionsWindowViewModel.ResolveTabIndex"/>의
/// 매핑도 함께 갱신해야 한다.
/// </summary>
public static class OptionsTabKeys
{
    public const string UserFolders = nameof(UserFolders);
    public const string DataDirectory = nameof(DataDirectory);
    public const string Certificate = nameof(Certificate);
    public const string DeviceSharing = nameof(DeviceSharing);
    public const string Compatibility = nameof(Compatibility);
    public const string Diagnostics = nameof(Diagnostics);
}

[Obsolete("This class is reserved for design-time usage.", false)]
public partial class OptionsWindowViewModelForDesigner : OptionsWindowViewModel { }

public partial class OptionsWindowViewModel : ObservableObject
{
    protected OptionsWindowViewModel() { }

    [ActivatorUtilitiesConstructor]
    public OptionsWindowViewModel(
        IPreferencesManager preferencesManager,
        IAppRestartManager appRestartManager,
        IAppMessageBox appMessageBox,
        ISharedLocations sharedLocations,
        TaskFactory taskFactory)
    {
        _preferencesManager = preferencesManager;
        _appRestartManager = appRestartManager;
        _appMessageBox = appMessageBox;
        _sharedLocations = sharedLocations;
        _taskFactory = taskFactory;
    }

    public event EventHandler? CloseRequested;

    public async Task RequestCloseAsync(object sender, EventArgs e, CancellationToken cancellationToken = default)
        => await _taskFactory.StartNew(() => CloseRequested?.Invoke(sender, e), cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// 탭 키 문자열을 <see cref="InitialTabIndex"/>로 적용한다. XAML의 TabControl에 정의된
    /// 탭 순서(사용자 폴더 / 데이터 디렉터리 / 인증서 / 장치 공유 / 호환성 / 진단)와 동기화되어야 한다.
    /// </summary>
    public void SetInitialTab(string? tabKey)
    {
        InitialTabIndex = ResolveTabIndex(tabKey);
    }

    private static int ResolveTabIndex(string? tabKey)
    {
        return tabKey switch
        {
            OptionsTabKeys.UserFolders => 0,
            OptionsTabKeys.DataDirectory => 1,
            OptionsTabKeys.Certificate => 2,
            OptionsTabKeys.DeviceSharing => 3,
            OptionsTabKeys.Compatibility => 4,
            OptionsTabKeys.Diagnostics => 5,
            _ => 0,
        };
    }

    [RelayCommand]
    private async Task OptionsWindowLoaded()
    {
        var currentConfig = await _preferencesManager.LoadPreferencesAsync();
        currentConfig ??= _preferencesManager.GetDefaultPreferences();

        _suppressSave = true;
        try
        {
            EnableMicrophone = currentConfig.UseAudioRedirection;
            EnableWebCam = currentConfig.UseVideoRedirection;
            EnablePrinters = currentConfig.UsePrinterRedirection;
            EnableLogAutoCollecting = currentConfig.UseLogCollection;
            ShareNpkiFolder = currentConfig.ShareNpkiFolder;
            EnableSandboxGpuAcceleration = currentConfig.EnableSandboxGpuAcceleration;

            RefreshDataDirectoryDisplay(currentConfig.DataDirectoryHostPath);

            // 사용자 폴더 목록을 환경 설정에서 로드하고 가용성을 검증해 화면에 표시한다.
            MappedFolders.Clear();
            foreach (var folder in currentConfig.MappedFolders ?? new List<MappedFolderSetting>())
            {
                folder.IsUnavailable = !FolderIsAccessible(folder.HostFolder);
                MappedFolders.Add(folder);
            }
        }
        finally
        {
            _suppressSave = false;
        }

        PropertyChanged += ViewModel_PropertyChanged;
    }

    [RelayCommand]
    private async Task CloseDialog()
    {
        await RequestCloseAsync(this, EventArgs.Empty);
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
            IsUnavailable = !FolderIsAccessible(selectedPath),
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

        // MappedFolderSetting은 ObservableObject가 아니므로 ListBox 갱신을 위해 항목을 재배치한다.
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
    private async Task SelectDataDirectory()
    {
        var selectedPath = ShowFolderBrowserDialog(UIStringResources.QuickStart_DataDirectory_SelectFolderTitle);

        if (string.IsNullOrWhiteSpace(selectedPath))
            return;

        await SaveDataDirectoryAsync(selectedPath);
        RefreshDataDirectoryDisplay(selectedPath);
    }

    [RelayCommand]
    private async Task ResetDataDirectory()
    {
        await SaveDataDirectoryAsync(null);
        RefreshDataDirectoryDisplay(null);
    }

    [RelayCommand]
    private void OpenDataDirectory()
    {
        var path = DataDirectoryDisplayPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        // 폴더가 아직 없을 때 부모 폴더를 대신 여는 혼란을 막는다.
        // 존재하지 않으면 명확히 알리고, 사용자가 원하면 만들어서 연다.
        if (!Directory.Exists(path))
        {
            var prompt = string.Format(UIStringResources.QuickStart_DataDirectory_OpenMissingPrompt, path);
            if (_appMessageBox.DisplayInfo(prompt, MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                _appMessageBox.DisplayError(ex, false);
                return;
            }
        }

        OpenInExplorer(path);
    }

    /// <summary>
    /// 환경 설정의 Data 경로 설정값을 화면 상태(현재 경로/기본값 여부/비로컬 경고)로 반영한다.
    /// <paramref name="configuredPath"/>가 비어 있으면 호스트 기본 Data 경로가 표시된다.
    /// </summary>
    private void RefreshDataDirectoryDisplay(string? configuredPath)
    {
        DataDirectoryDisplayPath = _sharedLocations.GetEffectiveDataDirectoryPath(configuredPath);
        DataDirectoryIsCustom = !string.IsNullOrWhiteSpace(configuredPath);
        DataDirectoryIsNonLocal = !_sharedLocations.IsMountableDataDirectory(DataDirectoryDisplayPath);
    }

    private async Task SaveDataDirectoryAsync(string? hostPath)
    {
        var currentConfig = await _preferencesManager.LoadPreferencesAsync();
        currentConfig ??= _preferencesManager.GetDefaultPreferences();

        currentConfig.DataDirectoryHostPath = string.IsNullOrWhiteSpace(hostPath) ? null : hostPath;
        await _preferencesManager.SavePreferencesAsync(currentConfig);
    }

    private static void OpenInExplorer(string path)
    {
        try
        {
            // 호출 측(OpenDataDirectory)이 존재를 보장한 뒤에만 부른다. 만에 하나 사라졌으면
            // 엉뚱한 폴더를 여는 대신 조용히 무시한다.
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch
        {
            // 탐색기 열기는 보조 동작이므로 실패해도 조용히 무시한다.
        }
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

    private async Task SaveMappedFoldersAsync()
    {
        var currentConfig = await _preferencesManager.LoadPreferencesAsync();
        currentConfig ??= _preferencesManager.GetDefaultPreferences();

        currentConfig.MappedFolders = MappedFolders.ToList();
        await _preferencesManager.SavePreferencesAsync(currentConfig);
    }

    [ObservableProperty]
    private bool _enableMicrophone;

    [ObservableProperty]
    private bool _enableWebCam;

    [ObservableProperty]
    private bool _enablePrinters;

    [ObservableProperty]
    private bool _enableLogAutoCollecting;

    [ObservableProperty]
    private bool _shareNpkiFolder;

    [ObservableProperty]
    private bool _enableSandboxGpuAcceleration;

    /// <summary>
    /// 데이터 디렉터리 탭에 표시되는 현재 유효 Data 경로(설정값이 없으면 호스트 기본 경로).
    /// </summary>
    [ObservableProperty]
    private string _dataDirectoryDisplayPath = string.Empty;

    /// <summary>
    /// 현재 Data 경로가 사용자가 직접 지정한 경로인지 여부. true일 때만 "기본값으로" 버튼이 활성화된다.
    /// </summary>
    [ObservableProperty]
    private bool _dataDirectoryIsCustom;

    /// <summary>
    /// 현재 Data 경로가 로컬 고정 디스크가 아닌 곳에 있어 샌드박스가 마운트하지 못할 수 있는지 여부.
    /// true이면 데이터 디렉터리 탭에 경고 문구를 노출한다.
    /// </summary>
    [ObservableProperty]
    private bool _dataDirectoryIsNonLocal;

    /// <summary>
    /// 옵션 창이 처음 열릴 때 선택될 탭 인덱스. <see cref="OptionsTabKeys"/>의 키와 매핑된다.
    /// 호출 측이 지정하지 않으면 0(첫 탭 = 사용자 폴더).
    /// </summary>
    [ObservableProperty]
    private int _initialTabIndex;

    /// <summary>
    /// 인증서 탭의 설명 문구. 리소스 문자열에 포함된 <c>%USERPROFILE%</c> 같은 환경 변수를
    /// 실제 경로로 치환하여 사용자가 자신의 경로를 곧바로 확인할 수 있게 한다.
    /// </summary>
    public string CertificateSectionDescription { get; }
        = Environment.ExpandEnvironmentVariables(UIStringResources.Options_Section_Certificate_Description);

    [ObservableProperty]
    private ObservableCollection<MappedFolderSetting> _mappedFolders = new();

    [ObservableProperty]
    private MappedFolderSetting? _selectedMappedFolder;

    private bool _suppressSave;
    private readonly IPreferencesManager _preferencesManager = default!;
    private readonly IAppRestartManager _appRestartManager = default!;
    private readonly IAppMessageBox _appMessageBox = default!;
    private readonly ISharedLocations _sharedLocations = default!;
    private readonly TaskFactory _taskFactory = default!;

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        => OnViewModelPropertyChangedAsync(sender, e).SafeFireAndForget();

    private async Task OnViewModelPropertyChangedAsync(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressSave)
            return;

        var viewModel = sender as OptionsWindowViewModel;
        ArgumentNullException.ThrowIfNull(viewModel);

        var currentConfig = await _preferencesManager.LoadPreferencesAsync();
        currentConfig ??= _preferencesManager.GetDefaultPreferences();

        var reserveRestart = false;

        switch (e.PropertyName)
        {
            case nameof(EnableMicrophone):
                currentConfig.UseAudioRedirection = viewModel.EnableMicrophone;
                break;

            case nameof(EnableWebCam):
                currentConfig.UseVideoRedirection = viewModel.EnableWebCam;
                break;

            case nameof(EnablePrinters):
                currentConfig.UsePrinterRedirection = viewModel.EnablePrinters;
                break;

            case nameof(EnableLogAutoCollecting):
                currentConfig.UseLogCollection = viewModel.EnableLogAutoCollecting;
                reserveRestart = _appRestartManager.AskRestart();
                break;

            case nameof(ShareNpkiFolder):
                currentConfig.ShareNpkiFolder = viewModel.ShareNpkiFolder;
                break;

            case nameof(EnableSandboxGpuAcceleration):
                currentConfig.EnableSandboxGpuAcceleration = viewModel.EnableSandboxGpuAcceleration;
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
