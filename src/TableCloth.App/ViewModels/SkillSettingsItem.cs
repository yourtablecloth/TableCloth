using System;
using TableCloth.ManagedAi;

namespace TableCloth.ViewModels;

public sealed record SkillSettingsItem(AiSkill Skill, SkillCatalogEntry? CatalogEntry = null)
{
    public string Id => Skill.Id;
    public string Name => Skill.Name;
    public string Description => Skill.IsValid ? Skill.Description : ManagedAiText.Select(
        "SKILL.md를 읽을 수 없습니다. 이 스킬을 비활성화하거나 제거한 뒤 다시 가져올 수 있습니다.",
        "Could not read SKILL.md. You can disable the skill or remove it and import it again.");
    public string Directory => Skill.Directory;
    public bool Enabled => Skill.Enabled;
    public bool IsValid => Skill.IsValid;
    public bool HasSource => Skill.Source is not null;
    public bool MatchesCatalog => CatalogEntry is not null && SourceMatches(Skill, CatalogEntry);
    public bool CanUpdate => MatchesCatalog && !string.Equals(Skill.Source!.SourceRef, CatalogEntry!.SourceRef, StringComparison.OrdinalIgnoreCase);
    public string VersionLabel => !HasSource ? ManagedAiText.Select("로컬 스킬", "Local skill")
        : CatalogEntry is null ? ManagedAiText.Select("카탈로그에서 확인할 수 없음", "Catalog entry unavailable")
        : !MatchesCatalog ? ManagedAiText.Select("카탈로그와 출처 다름", "Different catalog source")
        : CanUpdate ? ManagedAiText.Select("변경 버전 있음", "Different catalog revision")
        : ManagedAiText.Select("최신 (카탈로그 기준)", "Latest catalog revision");
    public string InstalledRevision => InstalledRevisionText(Skill);
    public string CatalogRevision => CatalogRevisionText(CatalogEntry);
    public string Source => Skill.Source is { } source ? source.Repository + "\n" + source.SourcePath : string.Empty;
    public string VersionDescription => !HasSource ? ManagedAiText.Select(
        "로컬에서 가져온 스킬은 카탈로그의 같은 ID로 덮어쓰지 않습니다. 기존 스킬을 제거한 뒤 카탈로그에서 설치할 수 있습니다.",
        "A local skill is not overwritten by a catalog entry with the same ID. You can remove it before installing from the catalog.")
        : CatalogEntry is null ? ManagedAiText.Select("카탈로그에서 스킬을 확인할 수 없습니다. 설치된 스킬의 사용 설정과 제거는 계속 관리할 수 있습니다.",
            "A current catalog entry is unavailable. You can continue managing its enabled state and removing it.")
        : !MatchesCatalog ? ManagedAiText.Select("설치된 스킬과 카탈로그의 출처가 달라 업데이트하지 않습니다.", "The installed skill has a different source and cannot be updated from this catalog entry.")
        : CanUpdate ? ManagedAiText.Select("카탈로그의 커밋이 설치한 커밋과 다릅니다. 업데이트하면 사용 설정을 유지하고 스킬 파일을 교체합니다.",
            "The catalog commit differs from the installed commit. Updating replaces skill files and keeps the enabled state.")
        : ManagedAiText.Select("설치한 커밋이 현재 카탈로그의 커밋과 일치합니다.", "The installed commit matches the current catalog commit.");
    public string StateLabel => !IsValid ? ManagedAiText.Select("SKILL.md 확인 필요", "Invalid SKILL.md")
        : Enabled ? ManagedAiText.Select("사용 중", "Enabled") : ManagedAiText.Select("사용 안 함", "Disabled");

    internal static bool SourceMatches(AiSkill skill, SkillCatalogEntry entry) => skill.Source is { } source
        && string.Equals(source.CatalogId, entry.Id, StringComparison.OrdinalIgnoreCase)
        && string.Equals(source.Repository, entry.Repository, StringComparison.OrdinalIgnoreCase)
        && string.Equals(source.SourcePath, entry.SourcePath, StringComparison.Ordinal);

    internal static string InstalledRevisionText(AiSkill? skill) => ManagedAiText.Select(
        $"설치한 커밋: {skill?.Source?.SourceRef ?? "로컬 / 기록 없음"}",
        $"Installed commit: {skill?.Source?.SourceRef ?? "Local / untracked"}");
    internal static string CatalogRevisionText(SkillCatalogEntry? entry) => ManagedAiText.Select(
        $"카탈로그 커밋: {entry?.SourceRef ?? "확인할 수 없음"}",
        $"Catalog commit: {entry?.SourceRef ?? "Unavailable"}");
}
