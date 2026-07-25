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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
    public const string Preview = nameof(Preview);
}

[Obsolete("This class is reserved for design-time usage.", false)]
public partial class OptionsWindowViewModelForDesigner : OptionsWindowViewModel { }

public partial class OptionsWindowViewModel : ObservableObject
{
    protected OptionsWindowViewModel()
    {
        BuildCompatibilityOptions();
    }

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

        BuildCompatibilityOptions();
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
            OptionsTabKeys.Preview => 6,
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
            EnableSandboxPublicDnsFallback = currentConfig.EnableSandboxPublicDnsFallback;
            EnableZScalerRootCertPropagation = currentConfig.EnableZScalerRootCertPropagation;

            // [미리 보기] 유휴 자동 종료(이슈 #197). 값이 옵션 목록에 없으면 가장 가까운 기본값으로 보정.
            EnableIdleAutoLogout = currentConfig.EnableIdleAutoLogout;
            IdleAutoLogoutMinutes = IdleTimeoutOptions.Contains(currentConfig.IdleAutoLogoutMinutes)
                ? currentConfig.IdleAutoLogoutMinutes
                : 10;

            // 이슈 #296: 업데이트 릴리스 링(안정/미리 보기).
            SelectedUpdateChannel = currentConfig.UpdateChannel;

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

    // 공용 DNS 폴백(이슈 #285). 기본 켜짐 — 게스트 DNS 해석 실패 시에만 공용 DNS로 폴백(probe-then-fallback).
    [ObservableProperty]
    private bool _enableSandboxPublicDnsFallback = true;

    // ZScaler 루트 인증서 전파(이슈 #292). 기본 꺼짐 — SSL 검사(ZScaler) 환경에서만 켠다. 무설치 미지원.
    [ObservableProperty]
    private bool _enableZScalerRootCertPropagation;

    // --- 호환성 탭: 검색 가능한 설정 목록 ---
    // 호스트 보안 프로그램 대응 등으로 호환성 설정이 계속 늘어날 수 있어, 하드코딩된 체크박스 대신
    // 항목 컬렉션 + 검색 필터 + 수직 스크롤로 구성한다. 각 항목은 위의 대응 bool 속성을 그대로 읽고 쓰는
    // 얇은 어댑터이므로 저장 파이프라인(OnViewModelPropertyChangedAsync)은 변경 없이 재사용된다.

    /// <summary>호환성 탭 설정 항목 원본(필터 전 전체). 검색 결과는 <see cref="CompatibilityOptions"/>에 반영된다.</summary>
    private readonly List<CompatibilityOptionItem> _allCompatibilityOptions = new();

    /// <summary>검색어로 필터링되어 실제로 화면에 표시되는 호환성 설정 항목.</summary>
    public ObservableCollection<CompatibilityOptionItem> CompatibilityOptions { get; } = new();

    /// <summary>호환성 탭 검색어. 이름·설명의 한국어·영어 텍스트에 부분 일치하는 항목만 표시된다.</summary>
    [ObservableProperty]
    private string _compatibilityOptionSearchText = string.Empty;

    /// <summary>현재 검색어와 일치하는 호환성 설정이 하나도 없는지 여부. “결과 없음” 안내 표시에 사용한다.</summary>
    [ObservableProperty]
    private bool _hasNoCompatibilityMatches;

    // 검색창 워터마크(placeholder)는 뷰(CueBannerBrush, 빈 값 트리거)가 직접 처리하므로 별도 상태를 두지 않는다.
    partial void OnCompatibilityOptionSearchTextChanged(string value)
        => ApplyCompatibilityFilter();

    /// <summary>
    /// 호환성 탭에 노출할 설정 항목을 구성한다. 새 호환성 설정을 추가할 때는 대응 bool
    /// <c>[ObservableProperty]</c>를 만들고 여기에 항목 하나만 등록하면 검색·스크롤 UI에 자동 편입된다.
    /// </summary>
    private void BuildCompatibilityOptions()
    {
        _allCompatibilityOptions.Add(new CompatibilityOptionItem(
            this,
            nameof(EnableSandboxGpuAcceleration),
            UIStringResources.Option_EnableSandboxGpuAccelerationCheckbox,
            UIStringResources.Option_EnableSandboxGpuAccelerationCheckbox_Description,
            BuildSearchText(
                nameof(UIStringResources.Option_EnableSandboxGpuAccelerationCheckbox),
                nameof(UIStringResources.Option_EnableSandboxGpuAccelerationCheckbox_Description)),
            () => EnableSandboxGpuAcceleration,
            value => EnableSandboxGpuAcceleration = value));

        _allCompatibilityOptions.Add(new CompatibilityOptionItem(
            this,
            nameof(EnableSandboxPublicDnsFallback),
            UIStringResources.Option_EnableSandboxPublicDnsFallbackCheckbox,
            UIStringResources.Option_EnableSandboxPublicDnsFallbackCheckbox_Description,
            BuildSearchText(
                nameof(UIStringResources.Option_EnableSandboxPublicDnsFallbackCheckbox),
                nameof(UIStringResources.Option_EnableSandboxPublicDnsFallbackCheckbox_Description)),
            () => EnableSandboxPublicDnsFallback,
            value => EnableSandboxPublicDnsFallback = value));

        _allCompatibilityOptions.Add(new CompatibilityOptionItem(
            this,
            nameof(EnableZScalerRootCertPropagation),
            UIStringResources.Option_EnableZScalerRootCertPropagationCheckbox,
            UIStringResources.Option_EnableZScalerRootCertPropagationCheckbox_Description,
            BuildSearchText(
                nameof(UIStringResources.Option_EnableZScalerRootCertPropagationCheckbox),
                nameof(UIStringResources.Option_EnableZScalerRootCertPropagationCheckbox_Description)),
            () => EnableZScalerRootCertPropagation,
            value => EnableZScalerRootCertPropagation = value));

        ApplyCompatibilityFilter();
    }

    /// <summary>
    /// 지정한 리소스 키들의 한국어·영어 텍스트를 모두 합쳐 검색용 문자열을 만든다. 현재 UI 언어와
    /// 무관하게 한/영 어느 쪽으로 입력해도 검색되도록, 한국어 리소스와 중립(영어) 리소스를 함께 담는다.
    /// </summary>
    private static string BuildSearchText(params string[] resourceKeys)
    {
        var resourceManager = UIStringResources.ResourceManager;
        var builder = new StringBuilder();
        foreach (var key in resourceKeys)
        {
            builder.Append(resourceManager.GetString(key, KoreanCulture) ?? string.Empty).Append('\n');
            builder.Append(resourceManager.GetString(key, CultureInfo.InvariantCulture) ?? string.Empty).Append('\n');
        }
        return builder.ToString();
    }

    /// <summary>검색어에 맞춰 <see cref="CompatibilityOptions"/>를 다시 채우고 결과 없음 여부를 갱신한다.</summary>
    private void ApplyCompatibilityFilter()
    {
        CompatibilityOptions.Clear();
        foreach (var item in _allCompatibilityOptions)
        {
            if (item.MatchesFilter(CompatibilityOptionSearchText))
                CompatibilityOptions.Add(item);
        }
        HasNoCompatibilityMatches = CompatibilityOptions.Count == 0;
    }

    // [미리 보기] 유휴 자동 종료(이슈 #197). 기본 꺼짐 + 유휴 허용 시간(분).
    [ObservableProperty]
    private bool _enableIdleAutoLogout;

    [ObservableProperty]
    private int _idleAutoLogoutMinutes = 10;

    /// <summary>유휴 허용 시간(분) 선택지. ComboBox에 바인딩된다.</summary>
    public IReadOnlyList<int> IdleTimeoutOptions { get; } = new[] { 5, 10, 15, 30 };

    // 이슈 #296: 업데이트 릴리스 링(안정/미리 보기). 미리 보기 탭의 라디오 버튼과 EnumBooleanConverter 로 바인딩.
    // 변경 시 저장만 하며(재시작 불필요), 다음 업데이트 확인부터 선택한 채널이 적용된다.
    [ObservableProperty]
    private ReleaseChannel _selectedUpdateChannel = ReleaseChannel.Retail;

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

    // 검색 인덱스를 만들 때 한국어 리소스를 명시적으로 읽기 위한 문화권(현재 UI 언어와 무관하게 한/영 모두 색인).
    private static readonly CultureInfo KoreanCulture = CultureInfo.GetCultureInfo("ko");

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

        // 저장 대상이 아닌 UI 전용 상태(호환성 탭 검색어/결과 없음 플래그)는
        // 아래의 preferences 로드를 유발하지 않도록 즉시 무시한다(검색은 매 키 입력마다 발생).
        if (e.PropertyName is nameof(CompatibilityOptionSearchText)
            or nameof(HasNoCompatibilityMatches))
        {
            return;
        }

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

            case nameof(EnableSandboxPublicDnsFallback):
                currentConfig.EnableSandboxPublicDnsFallback = viewModel.EnableSandboxPublicDnsFallback;
                break;

            case nameof(EnableZScalerRootCertPropagation):
                currentConfig.EnableZScalerRootCertPropagation = viewModel.EnableZScalerRootCertPropagation;
                break;

            case nameof(EnableIdleAutoLogout):
                currentConfig.EnableIdleAutoLogout = viewModel.EnableIdleAutoLogout;
                break;

            case nameof(IdleAutoLogoutMinutes):
                currentConfig.IdleAutoLogoutMinutes = viewModel.IdleAutoLogoutMinutes;
                break;

            case nameof(SelectedUpdateChannel):
                currentConfig.UpdateChannel = viewModel.SelectedUpdateChannel;
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
