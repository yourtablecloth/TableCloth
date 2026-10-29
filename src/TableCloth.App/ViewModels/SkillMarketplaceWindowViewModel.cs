using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Components;
using TableCloth.ManagedAi;
using TableCloth.Models;

namespace TableCloth.ViewModels;

public sealed record SkillCategoryChoice(string Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record MarketplaceSkillItem(SkillCatalogEntry Entry, AiSkill? InstalledSkill)
{
    public bool Installed => InstalledSkill is not null;
    public bool CanUpdate => InstalledSkill is not null && new SkillSettingsItem(InstalledSkill, Entry).CanUpdate;
    public string Id => Entry.Id;
    public string Name => Entry.DisplayName;
    public string Description => Entry.Description;
    public string Category => ManagedAiText.SkillCategory(Entry.Category);
    public string State => InstalledSkill is not null ? new SkillSettingsItem(InstalledSkill, Entry).VersionLabel
        : ManagedAiText.Select("설치 가능", "Available");
    public string Author => ManagedAiText.Select($"저자: {Entry.Author}", $"Author: {Entry.Author}");
    public string License => ManagedAiText.Select($"라이선스: {Entry.LicenseSpdx}", $"License: {Entry.LicenseSpdx}");
    public string Requirements => ManagedAiText.Select(
        $"런타임: {Entry.Runtime} / 설치 방식: {Entry.InstallMode} / 네트워크: {ManagedAiText.SkillNetwork(Entry.Network)}",
        $"Runtime: {Entry.Runtime} / Install mode: {Entry.InstallMode} / Network: {ManagedAiText.SkillNetwork(Entry.Network)}");
    public string LicenseSource => string.IsNullOrEmpty(Entry.LicenseSource) ? "" : ManagedAiText.Select($"라이선스 근거: {Entry.LicenseSource}", $"License evidence: {Entry.LicenseSource}");
    public string Source => Entry.Repository + (string.IsNullOrEmpty(Entry.SourcePath) ? "" : "\n" + Entry.SourcePath);
    public string Notes => Entry.Notes;
    public string InstalledRevision => InstalledSkill is null ? ManagedAiText.Select("아직 설치하지 않았습니다.", "Not installed yet.") : SkillSettingsItem.InstalledRevisionText(InstalledSkill);
    public string CatalogRevision => SkillSettingsItem.CatalogRevisionText(Entry);
    public string VersionDescription => InstalledSkill is not null ? new SkillSettingsItem(InstalledSkill, Entry).VersionDescription
        : ManagedAiText.Select("스킬을 식탁보 AI 전용 폴더에 설치할 수 있습니다.", "You can install this skill into the dedicated TableCloth AI folder.");
}

/// <summary>
/// Owns UI state on its caller's UI synchronization context. Construct and invoke it on that context;
/// service calls run off the UI thread and their continuations return to the captured context.
/// </summary>
public partial class SkillMarketplaceWindowViewModel : ObservableObject, IDisposable
{
    private readonly IManagedAiSkillManager _manager;
    private readonly IManagedAiSkillMarketplace _marketplace;
    private readonly IAppMessageBox _messages;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _token;
    private IReadOnlyList<AiSkill> _installed = [];
    private IReadOnlyList<SkillCatalogEntry> _catalog = [];
    private bool _disposed;
    private bool _catalogAvailable;
    private TaskCompletionSource? _localCompletion;

    internal Task WaitForLocalOperationAsync() => _localCompletion?.Task ?? Task.CompletedTask;

    public SkillMarketplaceWindowViewModel(IManagedAiSkillManager manager, IManagedAiSkillMarketplace marketplace,
        IAppMessageBox messages, IManagedAiScriptRuntimeManager? scriptRuntimes = null)
    {
        _manager = manager;
        _marketplace = marketplace;
        _messages = messages;
        _scriptRuntimes = scriptRuntimes;
        _token = _lifetime.Token;
        Categories.Add(new("", ManagedAiText.Select("모든 분야", "All categories")));
        _selectedCategory = Categories[0];
    }

    public Func<Task<string?>>? PickFolderAsync { private get; set; }
    public ObservableCollection<MarketplaceSkillItem> CatalogSkills { get; } = new();
    public ObservableCollection<SkillSettingsItem> InstalledSkills { get; } = new();
    public ObservableCollection<SkillCategoryChoice> Categories { get; } = new();
    [ObservableProperty] private string _catalogSearch = string.Empty;
    [ObservableProperty] private string _installedSearch = string.Empty;
    [ObservableProperty] private SkillCategoryChoice? _selectedCategory;
    [ObservableProperty] private MarketplaceSkillItem? _selectedCatalogSkill;
    [ObservableProperty] private SkillSettingsItem? _selectedInstalledSkill;
    [ObservableProperty] private bool _catalogBusy;
    [ObservableProperty] private bool _localBusy;
    [ObservableProperty] private string _catalogNotice = string.Empty;
    [ObservableProperty] private string _localNotice = string.Empty;
    [ObservableProperty] private string _localActivityText = string.Empty;

    public bool HasSelectedCatalogSkill => SelectedCatalogSkill is not null;
    public bool HasSelectedInstalledSkill => SelectedInstalledSkill is not null;
    public bool IsBusy => CatalogBusy || LocalBusy || SkillRuntimeBusy;
    public bool HasLocalActivity => LocalBusy && LocalActivityText.Length > 0;
    public string CatalogActivityText => ManagedAiText.Select("스킬 카탈로그를 불러오고 있습니다.", "Loading the skill catalog.");
    public bool CatalogEmpty => !CatalogBusy && CatalogSkills.Count == 0;
    public bool InstalledEmpty => !LocalBusy && InstalledSkills.Count == 0;
    public string CatalogSummary => CatalogBusy ? CatalogActivityText
        : ManagedAiText.Select($"후보 {_catalog.Count}개, 표시 {CatalogSkills.Count}개", $"{_catalog.Count} candidates, {CatalogSkills.Count} shown");
    public string InstalledSummary => ManagedAiText.Select($"보유 스킬 {_installed.Count}개, 활성 스킬 {_installed.Count(x => x.Enabled)}개, 표시 {InstalledSkills.Count}개",
        $"{_installed.Count} installed, {_installed.Count(x => x.Enabled)} enabled, {InstalledSkills.Count} shown");
    public string ToggleLabel => SelectedInstalledSkill?.Enabled == true ? ManagedAiText.Select("비활성화", "Disable") : ManagedAiText.Select("활성화", "Enable");

    partial void OnCatalogSearchChanged(string value) => ApplyCatalogFilter();
    partial void OnInstalledSearchChanged(string value) => ApplyInstalledFilter();
    partial void OnSelectedCategoryChanged(SkillCategoryChoice? value) => ApplyCatalogFilter();
    partial void OnSelectedCatalogSkillChanged(MarketplaceSkillItem? value)
    {
        OnPropertyChanged(nameof(HasSelectedCatalogSkill));
        InstallSkillCommand.NotifyCanExecuteChanged();
        UpdateCatalogSkillCommand.NotifyCanExecuteChanged();
        OpenSourceCommand.NotifyCanExecuteChanged();
        OpenLicenseSourceCommand.NotifyCanExecuteChanged();
    }
    partial void OnSelectedInstalledSkillChanged(SkillSettingsItem? value)
    {
        OnPropertyChanged(nameof(HasSelectedInstalledSkill));
        OnPropertyChanged(nameof(ToggleLabel));
        NotifyLocalCommands();
    }
    partial void OnLocalActivityTextChanged(string value) => OnPropertyChanged(nameof(HasLocalActivity));
    partial void OnLocalBusyChanged(bool value)
    {
        NotifyLocalCommands();
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(HasLocalActivity));
        OnPropertyChanged(nameof(InstalledEmpty));
    }
    partial void OnCatalogBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CatalogEmpty));
        OnPropertyChanged(nameof(CatalogSummary));
        RefreshCatalogCommand.NotifyCanExecuteChanged();
        InstallSkillCommand.NotifyCanExecuteChanged();
        UpdateCatalogSkillCommand.NotifyCanExecuteChanged();
        UpdateInstalledSkillCommand.NotifyCanExecuteChanged();
    }

    private void NotifyLocalCommands()
    {
        ImportSkillCommand.NotifyCanExecuteChanged();
        RefreshInstalledCommand.NotifyCanExecuteChanged();
        ToggleSkillCommand.NotifyCanExecuteChanged();
        RemoveSkillCommand.NotifyCanExecuteChanged();
        OpenSelectedFolderCommand.NotifyCanExecuteChanged();
        OpenRootFolderCommand.NotifyCanExecuteChanged();
        InstallSkillRuntimesCommand.NotifyCanExecuteChanged();
        InstallSkillCommand.NotifyCanExecuteChanged();
        UpdateCatalogSkillCommand.NotifyCanExecuteChanged();
        UpdateInstalledSkillCommand.NotifyCanExecuteChanged();
    }

    private bool CanUseLocal() => !LocalBusy && !_disposed;
    private bool CanUseSelectedLocal() => CanUseLocal() && SelectedInstalledSkill is { } item && InstalledSkills.Contains(item);
    private bool CanToggleSelected() => CanUseSelectedLocal() && SelectedInstalledSkill is { } item && (item.IsValid || item.Enabled);
    private bool CanRefreshCatalog() => !CatalogBusy && !_disposed;
    private bool CanInstall() => CanUseLocal() && !CatalogBusy && SelectedCatalogSkill is { Installed: false } item && CatalogSkills.Contains(item);
    private bool CanUpdateCatalog() => CanUseLocal() && !CatalogBusy && SelectedCatalogSkill is { CanUpdate: true } item && CatalogSkills.Contains(item);
    private bool CanUpdateInstalled() => CanUseSelectedLocal() && !CatalogBusy && SelectedInstalledSkill is { CanUpdate: true };
    private bool CanOpenSource() => !_disposed && SelectedCatalogSkill is { } item && CatalogSkills.Contains(item) && TryGetSourceUri(item.Entry, out _);
    private bool CanOpenLicenseSource() => !_disposed && SelectedCatalogSkill is { } item && CatalogSkills.Contains(item) && TryGetLicenseUri(item.Entry, out _);

    public Task LoadAsync() => Task.WhenAll(RefreshInstalled(), RefreshCatalog(), RefreshSkillRuntimeStatus());

    [RelayCommand(CanExecute = nameof(CanRefreshCatalog))]
    private async Task RefreshCatalog()
    {
        if (!CanRefreshCatalog()) return;
        CatalogBusy = true;
        CatalogNotice = ManagedAiText.Select("원격 카탈로그를 불러오고 있습니다.", "Loading the remote catalog.");
        try
        {
            _catalog = await Task.Run(() => _marketplace.ListAsync(_token), _token);
            _catalogAvailable = true;
            var category = SelectedCategory?.Id ?? "";
            Categories.Clear();
            Categories.Add(new("", ManagedAiText.Select("모든 분야", "All categories")));
            foreach (var id in _catalog.Select(x => x.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
                Categories.Add(new(id, ManagedAiText.SkillCategory(id)));
            SelectedCategory = Categories.FirstOrDefault(x => string.Equals(x.Id, category, StringComparison.OrdinalIgnoreCase)) ?? Categories[0];
            ApplyCatalogFilter();
            ApplyInstalledFilter();
            CatalogNotice = ManagedAiText.Select("카탈로그를 새로 불러왔습니다.", "Catalog refreshed.");
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch
        {
            _catalogAvailable = false;
            _catalog = [];
            ApplyCatalogFilter();
            ApplyInstalledFilter();
            CatalogNotice = ManagedAiText.Select("원격 카탈로그를 불러오지 못했습니다. 설치된 스킬은 계속 관리할 수 있습니다.",
                "Could not load the remote catalog. You can continue managing installed skills.");
        }
        finally { CatalogBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanUseLocal))]
    private Task RefreshInstalled() => RunLocalAsync(() => _manager.ListAsync(_token),
        ManagedAiText.Select("설치된 스킬을 불러오고 있습니다.", "Loading installed skills."),
        ManagedAiText.Select("설치된 스킬을 새로 불러왔습니다.", "Installed skills refreshed."));

    [RelayCommand(CanExecute = nameof(CanUseLocal))]
    private async Task ImportSkill()
    {
        if (!CanUseLocal() || PickFolderAsync is null) return;
        LocalActivityText = ManagedAiText.Select("가져올 스킬 폴더를 선택하고 있습니다.", "Selecting a skill folder to import.");
        LocalBusy = true;
        string? source;
        try { source = await PickFolderAsync(); }
        catch { LocalNotice = ManagedAiText.Select("스킬 폴더를 선택하지 못했습니다.", "Could not select a skill folder."); return; }
        finally { LocalBusy = false; LocalActivityText = string.Empty; }
        if (!string.IsNullOrWhiteSpace(source)) await ImportFromDirectoryAsync(source);
    }

    public Task ImportFromDirectoryAsync(string source) => RunLocalAsync(() => _manager.ImportAsync(source, _token),
        ManagedAiText.Select("스킬을 전용 폴더로 가져오고 있습니다.", "Importing the skill into the dedicated folder."),
        ManagedAiText.Select("스킬을 전용 폴더로 가져왔습니다.", "Skill imported into the dedicated folder."));

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private Task InstallSkill()
    {
        if (!CanInstall()) return Task.CompletedTask;
        var selected = SelectedCatalogSkill!;
        return RunMarketplaceActionAsync(() => _marketplace.InstallAsync(selected.Id, _token),
            ManagedAiText.Select($"{selected.Name} 스킬을 설치하고 있습니다.", $"Installing {selected.Name}."),
            ManagedAiText.Select("스킬을 설치했습니다.", "Skill installed."), true);
    }

    [RelayCommand(CanExecute = nameof(CanUpdateCatalog))]
    private Task UpdateCatalogSkill()
    {
        if (!CanUpdateCatalog()) return Task.CompletedTask;
        var selected = SelectedCatalogSkill!;
        return UpdateAsync(selected.Id, selected.Name, true);
    }

    [RelayCommand(CanExecute = nameof(CanUpdateInstalled))]
    private Task UpdateInstalledSkill()
    {
        if (!CanUpdateInstalled()) return Task.CompletedTask;
        var selected = SelectedInstalledSkill!;
        return UpdateAsync(selected.Id, selected.Name);
    }

    private Task UpdateAsync(string id, string name, bool fromCatalog = false)
    {
        var success = ManagedAiText.Select("스킬 파일을 카탈로그의 커밋으로 업데이트했습니다.", "Skill files updated to the catalog commit.");
        return RunMarketplaceActionAsync(() => _marketplace.UpdateAsync(id, _token),
            ManagedAiText.Select($"{name} 스킬을 업데이트하고 있습니다.", $"Updating {name}."), success, fromCatalog);
    }

    private async Task RunMarketplaceActionAsync(Func<Task<IReadOnlyList<AiSkill>>> action, string activity, string success, bool fromCatalog)
    {
        if (await RunLocalAsync(action, activity, success))
        {
            await RefreshCatalog();
            LocalNotice = _catalogAvailable ? success : success + " " + CatalogNotice;
        }
        if (fromCatalog) CatalogNotice = LocalNotice;
    }

    [RelayCommand(CanExecute = nameof(CanToggleSelected))]
    private Task ToggleSkill()
    {
        if (!CanToggleSelected()) return Task.CompletedTask;
        var selected = SelectedInstalledSkill!;
        return RunLocalAsync(() => _manager.SetEnabledAsync(selected.Id, !selected.Enabled, _token),
            ManagedAiText.Select($"{selected.Name} 스킬 사용 설정을 변경하고 있습니다.", $"Updating the enabled state of {selected.Name}."),
            ManagedAiText.Select("스킬 사용 설정을 변경했습니다. 다음 대화부터 적용합니다.", "Skill state updated. The next chat uses this setting."));
    }

    [RelayCommand(CanExecute = nameof(CanUseSelectedLocal))]
    private Task RemoveSkill()
    {
        if (!CanUseSelectedLocal()) return Task.CompletedTask;
        var selected = SelectedInstalledSkill!;
        if (_messages.DisplayQuestion(ManagedAiText.Select($"{selected.Name} 스킬과 전용 폴더의 파일을 삭제하시겠습니까?",
            $"Delete the {selected.Name} skill and its files from the dedicated folder?"), AppMessageBoxButton.YesNo, AppMessageBoxResult.No) != AppMessageBoxResult.Yes)
            return Task.CompletedTask;
        // A modal confirmation can run a nested UI loop. Recheck the captured selection before mutation.
        if (!CanUseSelectedLocal() || !ReferenceEquals(selected, SelectedInstalledSkill)) return Task.CompletedTask;
        return RunLocalAsync(() => _manager.RemoveAsync(selected.Id, _token),
            ManagedAiText.Select($"{selected.Name} 스킬을 제거하고 있습니다.", $"Removing {selected.Name}."),
            ManagedAiText.Select("스킬을 제거했습니다.", "Skill removed."));
    }

    private async Task<bool> RunLocalAsync(Func<Task<IReadOnlyList<AiSkill>>> action, string activity, string success)
    {
        if (!CanUseLocal()) return false;
        var succeeded = false;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _localCompletion = completion;
        LocalActivityText = activity;
        LocalBusy = true;
        LocalNotice = activity;
        try
        {
            _installed = await Task.Run(action, _token);
            ApplyInstalledFilter();
            ApplyCatalogFilter();
            LocalNotice = success;
            succeeded = true;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (OperationCanceledException) { LocalNotice = ManagedAiText.Select("스킬 작업을 취소했습니다.", "Skill operation canceled."); }
        catch (ManagedAiException ex)
        {
            LocalNotice = ex.Code switch
            {
                AiFailureCode.SkillAlreadyInstalled => ManagedAiText.Select("같은 ID의 스킬이 이미 있습니다.", "A skill with this ID already exists."),
                AiFailureCode.SkillInvalid or AiFailureCode.InvalidPath or AiFailureCode.SkillIsolationFailed => ManagedAiText.Select("스킬 폴더나 SKILL.md를 확인할 수 없습니다.", "Could not validate the skill folder or SKILL.md."),
                AiFailureCode.RuntimeBusy => ManagedAiText.Select("다른 창에서 AI 작업을 진행하고 있습니다. 잠시 후 다시 시도할 수 있습니다.", "Another window is using AI. Try again shortly."),
                AiFailureCode.BlockedByPolicy => ManagedAiText.Select("전용 Codex 설정이나 카탈로그 설치 조건이 정책 검사를 통과하지 못했습니다.", "The dedicated Codex configuration or catalog installation conditions failed the policy check."),
                _ => ManagedAiText.Select("스킬 작업을 완료하지 못했습니다. 잠시 후 다시 시도할 수 있습니다.", "Could not complete the skill operation. Try again shortly.")
            };
        }
        catch { LocalNotice = ManagedAiText.Select("스킬 작업을 완료하지 못했습니다. 잠시 후 다시 시도할 수 있습니다.", "Could not complete the skill operation. Try again shortly."); }
        finally { LocalBusy = false; LocalActivityText = string.Empty; completion.TrySetResult(); }
        return succeeded;
    }

    private void ApplyCatalogFilter()
    {
        var selected = SelectedCatalogSkill?.Id;
        var search = CatalogSearch.Trim();
        var category = SelectedCategory?.Id ?? "";
        CatalogSkills.Clear();
        foreach (var entry in _catalog.Where(x => (category.Length == 0 || string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase))
            && Matches(search, x.Id, x.DisplayName, x.Description, x.Author, x.Category, ManagedAiText.SkillCategory(x.Category), x.Notes)))
            CatalogSkills.Add(new(entry, _installed.FirstOrDefault(x => string.Equals(x.Id, entry.Id, StringComparison.OrdinalIgnoreCase))));
        SelectedCatalogSkill = CatalogSkills.FirstOrDefault(x => string.Equals(x.Id, selected, StringComparison.OrdinalIgnoreCase));
        OnPropertyChanged(nameof(CatalogSummary));
        OnPropertyChanged(nameof(CatalogEmpty));
    }

    private void ApplyInstalledFilter()
    {
        var selected = SelectedInstalledSkill?.Id;
        var search = InstalledSearch.Trim();
        InstalledSkills.Clear();
        foreach (var item in _installed.Where(x => Matches(search, x.Id, x.Name, x.Description)))
            InstalledSkills.Add(new(item, _catalog.FirstOrDefault(x => string.Equals(x.Id, item.Id, StringComparison.OrdinalIgnoreCase))));
        SelectedInstalledSkill = InstalledSkills.FirstOrDefault(x => string.Equals(x.Id, selected, StringComparison.OrdinalIgnoreCase));
        OnPropertyChanged(nameof(InstalledSummary));
        OnPropertyChanged(nameof(InstalledEmpty));
    }

    private static bool Matches(string search, params string[] values) => search.Length == 0 || values.Any(x => x.Contains(search, StringComparison.OrdinalIgnoreCase));

    [RelayCommand]
    private void OpenContributionPage()
    {
        if (_disposed) return;
        try { Process.Start(new ProcessStartInfo("https://catalog.yourtablecloth.app/skills/") { UseShellExecute = true })?.Dispose(); }
        catch { _messages.DisplayError(ManagedAiText.Select("스킬 카탈로그 페이지를 열지 못했습니다.", "Could not open the skill catalog page."), false); }
    }

    [RelayCommand(CanExecute = nameof(CanOpenSource))]
    private void OpenSource()
    {
        if (!CanOpenSource() || SelectedCatalogSkill is not { } item || !TryGetSourceUri(item.Entry, out var target)) return;
        try { Process.Start(new ProcessStartInfo(target.AbsoluteUri) { UseShellExecute = true })?.Dispose(); }
        catch { CatalogNotice = ManagedAiText.Select("원본 페이지를 열지 못했습니다.", "Could not open the upstream page."); }
    }

    [RelayCommand(CanExecute = nameof(CanOpenLicenseSource))]
    private void OpenLicenseSource()
    {
        if (!CanOpenLicenseSource() || SelectedCatalogSkill is not { } item || !TryGetLicenseUri(item.Entry, out var target)) return;
        try { Process.Start(new ProcessStartInfo(target.AbsoluteUri) { UseShellExecute = true })?.Dispose(); }
        catch { CatalogNotice = ManagedAiText.Select("라이선스 원문을 열지 못했습니다.", "Could not open the license source."); }
    }

    internal static bool TryGetLicenseUri(SkillCatalogEntry entry, out Uri target)
    {
        target = null!;
        if (!TryGetSourceUri(entry with { SourcePath = "", SourceRef = "" }, out var repository)
            || entry.SourceRef.Length != 40 || !entry.SourceRef.All(char.IsAsciiHexDigit)
            || !Uri.TryCreate(entry.LicenseSource, UriKind.Absolute, out var license)
            || license.Scheme != "https" || !license.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || !license.IsDefaultPort || license.UserInfo.Length != 0 || license.Query.Length != 0 || license.Fragment.Length != 0
            || !license.AbsolutePath.StartsWith(repository.AbsolutePath.TrimEnd('/') + "/blob/" + entry.SourceRef + "/", StringComparison.Ordinal)) return false;
        if (license.AbsolutePath.Split('/').Skip(1).Select(Uri.UnescapeDataString).Any(x => x.Length == 0 || x is "." or ".." || x.Any(c => c is '/' or '\\' || char.IsControl(c)))) return false;
        target = license;
        return true;
    }

    internal static bool TryGetSourceUri(SkillCatalogEntry entry, out Uri target)
    {
        target = null!;
        if (!Uri.TryCreate(entry.Repository, UriKind.Absolute, out var repository) || repository.Scheme != "https"
            || !repository.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || !repository.IsDefaultPort
            || repository.UserInfo.Length != 0 || repository.Query.Length != 0 || repository.Fragment.Length != 0) return false;
        var parts = repository.AbsolutePath.Trim('/').Split('/');
        if (parts.Length != 2 || parts.Any(x => x.Length == 0 || x.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
            || parts.Any(x => x is "." or "..")) return false;
        target = repository;
        // Open the repository for an unpinned candidate. Never invent an upstream branch name.
        if (entry.SourceRef.Length > 0 && entry.SourcePath.Length > 0)
        {
            if (entry.SourcePath == ".")
            {
                target = new Uri(repository.AbsoluteUri.TrimEnd('/') + "/tree/" + Uri.EscapeDataString(entry.SourceRef));
                return true;
            }
            var segments = entry.SourcePath.Replace('\\', '/').Split('/');
            if (segments.Any(x => x.Length == 0 || x is "." or "..")) return false;
            target = new Uri(repository.AbsoluteUri.TrimEnd('/') + "/tree/" + Uri.EscapeDataString(entry.SourceRef) + "/"
                + string.Join("/", segments.Select(Uri.EscapeDataString)));
        }
        return true;
    }

    [RelayCommand(CanExecute = nameof(CanUseSelectedLocal))]
    private void OpenSelectedFolder()
    {
        if (CanUseSelectedLocal()) OpenFolder(SelectedInstalledSkill!.Directory);
    }

    [RelayCommand(CanExecute = nameof(CanUseLocal))]
    private void OpenRootFolder()
    {
        if (CanUseLocal()) OpenFolder(_manager.StorageDirectory);
    }

    private void OpenFolder(string path)
    {
        try
        {
            if (!IsManagedFolder(_manager.StorageDirectory, path)) throw new IOException();
            Process.Start(new ProcessStartInfo(Path.GetFullPath(path)) { UseShellExecute = true })?.Dispose();
        }
        catch { LocalNotice = ManagedAiText.Select("전용 스킬 폴더를 열지 못했습니다.", "Could not open the dedicated skill folder."); }
    }

    internal static bool IsManagedFolder(string root, string path)
    {
        try
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            if (!path.Equals(root, StringComparison.OrdinalIgnoreCase) && !path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
            var current = new DirectoryInfo(path);
            if (!current.Exists) return false;
            // Check every ancestor as a junction above the storage root can redirect the folder too.
            while (current is not null)
            {
                if ((current.Attributes & FileAttributes.ReparsePoint) != 0) return false;
                current = current.Parent;
            }
            return true;
        }
        catch { return false; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
        NotifyLocalCommands();
        RefreshCatalogCommand.NotifyCanExecuteChanged();
        OpenSourceCommand.NotifyCanExecuteChanged();
        OpenLicenseSourceCommand.NotifyCanExecuteChanged();
        InstallSkillRuntimesCommand.NotifyCanExecuteChanged();
        RefreshSkillRuntimeStatusCommand.NotifyCanExecuteChanged();
    }
}
