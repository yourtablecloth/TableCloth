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
    public string StateLabel => Enabled ? "사용 중" : "사용 안 함";
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
    private string _skillSummary = "AI 스킬 목록을 불러오고 있습니다.";

    [ObservableProperty]
    private string _skillNotice = string.Empty;

    public string SkillToggleLabel => SelectedAiSkill?.Enabled == true ? "비활성화" : "활성화";

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
        await RunSkillActionAsync(() => _skillsManager.ListAsync(CancellationToken.None), "스킬 목록을 새로 불러왔습니다.");
    }

    [RelayCommand]
    private async Task AddAiSkill()
    {
        if (_skillsManager is null || SkillOperationBusy) return;
        var source = await PickFolderAsync("SKILL.md가 들어 있는 스킬 폴더 선택");
        if (string.IsNullOrWhiteSpace(source)) return;
        await RunSkillActionAsync(() => _skillsManager.ImportAsync(source, CancellationToken.None), "스킬을 가져왔습니다.");
    }

    [RelayCommand]
    private async Task ToggleAiSkill()
    {
        if (_skillsManager is null || SkillOperationBusy || SelectedAiSkill is not { } selected) return;
        await RunSkillActionAsync(() => _skillsManager.SetEnabledAsync(selected.Id, !selected.Enabled, CancellationToken.None),
            $"{selected.Name} 스킬의 사용 설정을 변경했습니다.");
    }

    [RelayCommand]
    private async Task RemoveAiSkill()
    {
        if (_skillsManager is null || SkillOperationBusy || SelectedAiSkill is not { } selected) return;
        var confirmed = _appMessageBox.DisplayQuestion($"{selected.Name} 스킬과 전용 폴더의 파일을 삭제하시겠습니까?",
            AppMessageBoxButton.YesNo, AppMessageBoxResult.No);
        if (confirmed != AppMessageBoxResult.Yes) return;
        await RunSkillActionAsync(() => _skillsManager.RemoveAsync(selected.Id, CancellationToken.None),
            $"{selected.Name} 스킬을 제거했습니다.");
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
        catch { SkillNotice = "전용 스킬 폴더를 열지 못했습니다."; }
    }

    private async Task RunSkillActionAsync(Func<Task<System.Collections.Generic.IReadOnlyList<AiSkill>>> action, string success)
    {
        SkillOperationBusy = true;
        SkillNotice = "전용 스킬을 확인하고 있습니다.";
        try
        {
            var skills = await action();
            var selectedId = SelectedAiSkill?.Id;
            AiSkills.Clear();
            foreach (var skill in skills) AiSkills.Add(new(skill));
            SelectedAiSkill = AiSkills.FirstOrDefault(x => x.Id == selectedId);
            SkillSummary = $"보유 스킬 {AiSkills.Count}개, 활성 스킬 {AiSkills.Count(x => x.Enabled)}개";
            SkillNotice = success;
            SkillsLoaded = true;
        }
        catch (ManagedAiException ex)
        {
            SkillNotice = ex.Code switch
            {
                AiFailureCode.SkillAlreadyInstalled => "같은 이름의 스킬이 이미 있습니다.",
                AiFailureCode.SkillInvalid => "스킬 폴더나 SKILL.md를 확인할 수 없습니다.",
                AiFailureCode.RuntimeBusy => "다른 창에서 AI 작업을 진행하고 있습니다. 잠시 후 다시 시도할 수 있습니다.",
                _ => "스킬 목록을 갱신하지 못했습니다. 잠시 후 다시 시도할 수 있습니다."
            };
        }
        catch { SkillNotice = "스킬 작업을 완료하지 못했습니다. 잠시 후 다시 시도할 수 있습니다."; }
        finally { SkillOperationBusy = false; }
    }
}
