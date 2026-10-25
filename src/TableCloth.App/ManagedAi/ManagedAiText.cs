using System.Globalization;

namespace TableCloth.ManagedAi;

public static class ManagedAiText
{
    public static bool IsKorean => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko";

    public static string Select(string korean, string english) => IsKorean ? korean : english;

    public static string SkillsTabTitle => Select("AI 스킬", "AI skills");
    public static string ProductTitle => Select("식탁보 AI (베타)", "TableCloth AI (Beta)");
    public static string SkillsTabDescription => Select(
        "원격 스킬 탐색과 설치된 AI 스킬 관리는 스킬 마켓플레이스에서 진행합니다. 활성화한 스킬은 다음 대화부터 사용할 수 있습니다.",
        "Browse remote skills and manage installed AI skills in the skill marketplace. Enabled skills are available in the next chat.");
    public static string SkillsTabScope => Select(
        "식탁보 AI 전용 Codex 폴더의 스킬을 관리합니다. 런타임을 재설치해도 스킬 폴더는 유지됩니다.",
        "The marketplace manages the dedicated TableCloth AI Codex skill folder. Reinstalling the runtime keeps the skill folder.");
    public static string SkillsRemove => Select("스킬 제거", "Remove skill");
    public static string SkillsRefresh => Select("목록 새로 고침", "Refresh list");
    public static string SkillsOpenFolder => Select("전용 폴더 열기", "Open dedicated folder");
    public static string SkillsMarketplace => Select("스킬 마켓플레이스 열기", "Open skill marketplace");
    public static string MarketplaceTitle => Select("스킬 마켓플레이스", "Skill marketplace");
    public static string MarketplaceDescription => Select(
        "원격 카탈로그의 후보를 살펴보고 식탁보 AI 전용 Codex 스킬을 관리합니다. 활성 스킬은 다음 대화부터 사용합니다.",
        "Browse remote candidates and manage Codex skills dedicated to TableCloth AI. Enabled skills are available in the next chat.");
    public static string MarketplaceScope => Select("관리 범위: 식탁보 전용 스킬 폴더", "Scope: TableCloth dedicated skill folder");
    public static string MarketplaceBrowse => Select("카탈로그 탐색", "Browse catalog");
    public static string MarketplaceInstalled => Select("설치된 스킬", "Installed skills");
    public static string MarketplaceSearch => Select("이름, 설명, 저자 검색", "Search name, description, author");
    public static string MarketplaceInstalledSearch => Select("설치된 스킬 검색", "Search installed skills");
    public static string MarketplaceInstall => Select("스킬 설치", "Install skill");
    public static string MarketplaceUpdate => Select("업데이트", "Update skill");
    public static string MarketplaceSource => Select("원본 열기", "Open upstream source");
    public static string MarketplaceLicenseSource => Select("라이선스 원문 열기", "Open license source");
    public static string MarketplaceSelectedFolder => Select("선택한 스킬 폴더 열기", "Open selected skill folder");
    public static string MarketplaceChoose => Select("스킬을 선택하면 출처와 설치 조건을 확인할 수 있습니다.", "Select a skill to view its source and installation conditions.");
    public static string MarketplaceInstalledChoose => Select("설치된 스킬을 선택하면 사용 설정과 파일을 관리할 수 있습니다.", "Select an installed skill to manage its enabled state and files.");
    public static string MarketplaceNoResults => Select("검색 조건에 맞는 스킬이 없습니다.", "No skills match the current filters.");
    public static string MarketplaceImport => Select("로컬 폴더 가져오기", "Import local folder");
    public static string MarketplaceClose => Select("닫기", "Close");

    public static string SkillCategory(string category) => category.ToLowerInvariant() switch
    {
        "finance" => Select("금융", "Finance"),
        "economics" or "economy" => Select("경제", "Economics"),
        "accounting" => Select("회계", "Accounting"),
        "legal" or "law" => Select("법률", "Legal"),
        "statistics" => Select("통계", "Statistics"),
        "procurement" => Select("조달", "Procurement"),
        "publicdata" or "public-data" => Select("공공데이터", "Public data"),
        "legislation" => Select("입법", "Legislation"),
        "realestate" or "real-estate" => Select("부동산", "Real estate"),
        "documents" or "document" => Select("문서", "Documents"),
        _ => category
    };
    public static string SkillNetwork(string network) => network.ToLowerInvariant() switch
    {
        "required" => Select("필요", "Required"),
        "optional" => Select("선택", "Optional"),
        "none" => Select("사용 안 함", "None"),
        _ => network
    };
}
