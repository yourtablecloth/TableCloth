using AsyncAwaitBestPractices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.ManagedAi;
using TableCloth.Models;

namespace TableCloth.ViewModels;

public sealed record SkillSettingsItem(AiSkill Skill)
{
    public string Id => Skill.Id;
    public string Name => Skill.Name;
    public string Description => Skill.Description;
    public string Directory => Skill.Directory;
    public bool Enabled => Skill.Enabled;
    public string StateLabel => Enabled ? ManagedAiText.Select("사용 중", "Enabled") : ManagedAiText.Select("사용 안 함", "Disabled");
}

public partial class OptionsWindowViewModel
{
    private readonly IManagedAiSkillManager? _skillsManager;
    private bool _optionsLoaded;
    private bool SkillsLoaded { get; set; }

    public ObservableCollection<SkillSettingsItem> AiSkills { get; } = new();

    [ObservableProperty]
    private SkillSettingsItem? _selectedAiSkill;

    [ObservableProperty]
    private bool _skillOperationBusy;

    [ObservableProperty]
    private string _skillSummary = ManagedAiText.Select("AI 스킬 목록을 불러오고 있습니다.", "Loading AI skills.");

    [ObservableProperty]
    private string _skillNotice = string.Empty;

    public string SkillToggleLabel => SelectedAiSkill?.Enabled == true
        ? ManagedAiText.Select("비활성화", "Disable") : ManagedAiText.Select("활성화", "Enable");

    partial void OnSelectedAiSkillChanged(SkillSettingsItem? value)
        => OnPropertyChanged(nameof(SkillToggleLabel));

    partial void OnInitialTabIndexChanged(int value)
    {
        if (_optionsLoaded && value == 7) RefreshSkillsAsync().SafeFireAndForget();
    }

    [RelayCommand]
    private Task RefreshSkillList() => RefreshSkillsAsync();

    private async Task RefreshSkillsAsync()
    {
        if (_skillsManager is null || SkillOperationBusy) return;
        await RunSkillActionAsync(() => _skillsManager.ListAsync(CancellationToken.None),
            ManagedAiText.Select("스킬 목록을 새로 불러왔습니다.", "Skill list refreshed."));
    }

    [RelayCommand]
    private async Task AddAiSkill()
    {
        if (_skillsManager is null || SkillOperationBusy) return;
        var source = await PickFolderAsync(ManagedAiText.Select("SKILL.md가 들어 있는 스킬 폴더 선택", "Select a skill folder containing SKILL.md"));
        if (string.IsNullOrWhiteSpace(source)) return;
        await RunSkillActionAsync(() => _skillsManager.ImportAsync(source, CancellationToken.None),
            ManagedAiText.Select("스킬을 가져왔습니다.", "Skill imported."));
    }

    [RelayCommand]
    private async Task ToggleAiSkill()
    {
        if (_skillsManager is null || SkillOperationBusy || SelectedAiSkill is not { } selected) return;
        await RunSkillActionAsync(() => _skillsManager.SetEnabledAsync(selected.Id, !selected.Enabled, CancellationToken.None),
            ManagedAiText.Select($"{selected.Name} 스킬의 사용 설정을 변경했습니다.", $"Updated the enabled state of {selected.Name}."));
    }

    [RelayCommand]
    private async Task RemoveAiSkill()
    {
        if (_skillsManager is null || SkillOperationBusy || SelectedAiSkill is not { } selected) return;
        var confirmed = _appMessageBox.DisplayQuestion(ManagedAiText.Select(
                $"{selected.Name} 스킬과 전용 폴더의 파일을 삭제하시겠습니까?",
                $"Delete the {selected.Name} skill and its files from the dedicated folder?"),
            AppMessageBoxButton.YesNo, AppMessageBoxResult.No);
        if (confirmed != AppMessageBoxResult.Yes) return;
        await RunSkillActionAsync(() => _skillsManager.RemoveAsync(selected.Id, CancellationToken.None),
            ManagedAiText.Select($"{selected.Name} 스킬을 제거했습니다.", $"Removed the {selected.Name} skill."));
    }

    [RelayCommand]
    private void OpenAiSkillFolder()
    {
        if (_skillsManager is null) return;
        try
        {
            Directory.CreateDirectory(_skillsManager.StorageDirectory);
            Process.Start(new ProcessStartInfo(_skillsManager.StorageDirectory) { UseShellExecute = true })?.Dispose();
        }
        catch { SkillNotice = ManagedAiText.Select("전용 스킬 폴더를 열지 못했습니다.", "Could not open the dedicated skill folder."); }
    }

    private async Task RunSkillActionAsync(Func<Task<System.Collections.Generic.IReadOnlyList<AiSkill>>> action, string success)
    {
        SkillOperationBusy = true;
        SkillNotice = ManagedAiText.Select("전용 스킬을 확인하고 있습니다.", "Checking dedicated skills.");
        try
        {
            var skills = await action();
            var selectedId = SelectedAiSkill?.Id;
            AiSkills.Clear();
            foreach (var skill in skills) AiSkills.Add(new(skill));
            SelectedAiSkill = AiSkills.FirstOrDefault(x => x.Id == selectedId);
            SkillSummary = ManagedAiText.Select(
                $"보유 스킬 {AiSkills.Count}개, 활성 스킬 {AiSkills.Count(x => x.Enabled)}개",
                $"{AiSkills.Count} skills, {AiSkills.Count(x => x.Enabled)} enabled");
            SkillNotice = success;
            SkillsLoaded = true;
        }
        catch (ManagedAiException ex)
        {
            SkillNotice = ex.Code switch
            {
                AiFailureCode.SkillAlreadyInstalled => ManagedAiText.Select("같은 이름의 스킬이 이미 있습니다.", "A skill with this name already exists."),
                AiFailureCode.SkillInvalid => ManagedAiText.Select("스킬 폴더나 SKILL.md를 확인할 수 없습니다.", "Could not read the skill folder or SKILL.md."),
                AiFailureCode.RuntimeBusy => ManagedAiText.Select("다른 창에서 AI 작업을 진행하고 있습니다. 잠시 후 다시 시도할 수 있습니다.", "Another window is using AI. Try again shortly."),
                AiFailureCode.BlockedByPolicy => ManagedAiText.Select("전용 Codex 설정이 정책 검사를 통과하지 못했습니다. 설정을 복구한 뒤 다시 시도할 수 있습니다.", "The dedicated Codex configuration failed its policy check. Restore the configuration and try again."),
                _ => ManagedAiText.Select("스킬 목록을 갱신하지 못했습니다. 잠시 후 다시 시도할 수 있습니다.", "Could not refresh skills. Try again shortly.")
            };
        }
        catch { SkillNotice = ManagedAiText.Select("스킬 작업을 완료하지 못했습니다. 잠시 후 다시 시도할 수 있습니다.", "Could not complete the skill operation. Try again shortly."); }
        finally { SkillOperationBusy = false; }
    }
}
