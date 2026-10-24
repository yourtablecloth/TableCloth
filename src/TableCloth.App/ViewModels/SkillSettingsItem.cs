using TableCloth.ManagedAi;

namespace TableCloth.ViewModels;

public sealed record SkillSettingsItem(AiSkill Skill)
{
    public string Id => Skill.Id;
    public string Name => Skill.Name;
    public string Description => Skill.IsValid ? Skill.Description : ManagedAiText.Select(
        "SKILL.md를 읽을 수 없습니다. 이 스킬을 비활성화하거나 제거한 뒤 다시 가져올 수 있습니다.",
        "Could not read SKILL.md. You can disable the skill or remove it and import it again.");
    public string Directory => Skill.Directory;
    public bool Enabled => Skill.Enabled;
    public bool IsValid => Skill.IsValid;
    public string StateLabel => !IsValid ? ManagedAiText.Select("SKILL.md 확인 필요", "Invalid SKILL.md")
        : Enabled ? ManagedAiText.Select("사용 중", "Enabled") : ManagedAiText.Select("사용 안 함", "Disabled");
}
