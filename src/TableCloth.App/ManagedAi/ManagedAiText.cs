using System.Globalization;

namespace TableCloth.ManagedAi;

public static class ManagedAiText
{
    public static bool IsKorean => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko";

    public static string Select(string korean, string english) => IsKorean ? korean : english;

    public static string SkillsTabTitle => Select("AI 스킬", "AI skills");
    public static string PreviewTitle => Select("식탁보 AI (Preview)", "TableCloth AI (Preview)");
    public static string SkillsTabDescription => Select(
        "식탁보 AI 전용 Codex 스킬을 관리합니다. 활성 스킬은 다음 대화에서 사용할 수 있습니다. 런타임을 재설치해도 스킬 폴더는 유지됩니다.",
        "Manage Codex skills dedicated to TableCloth AI. Enabled skills are available in the next chat. Reinstalling the runtime keeps the skill folder.");
    public static string SkillsAdd => Select("스킬 추가", "Add skill");
    public static string SkillsRemove => Select("스킬 제거", "Remove skill");
    public static string SkillsRefresh => Select("목록 새로 고침", "Refresh list");
    public static string SkillsOpenFolder => Select("전용 폴더 열기", "Open dedicated folder");
}
