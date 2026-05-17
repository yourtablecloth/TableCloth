using System;
using System.Reflection;
using TableCloth.Resources;

namespace Spork.Components
{
    /// <summary>
    /// Spork.App UI 가 사용자에게 보여줄 브랜드 표기를 entry 어셈블리에 따라 런타임에 결정한다.
    /// 같은 Spork.App 코드 베이스가 두 가지 진입점으로 호스팅되기 때문에 정적 리소스 매핑만으론
    /// 부족하다:
    /// <list type="bullet">
    ///   <item><c>TableCloth.exe</c> entry — 통합 바이너리의 spork verb 핸들러. UI 는 식탁보 브랜드.</item>
    ///   <item><c>Spork.exe</c> entry — 단독 출시(비-샌드박스 재단 릴리스 포함). UI 는 포카락 브랜드.</item>
    /// </list>
    /// 본 클래스는 첫 호출 시 <see cref="Assembly.GetEntryAssembly"/> 의 AssemblyName 으로 한 번 판정한
    /// 결과를 캐시하고, 각 속성이 식탁보/포카락 변형 리소스 중 알맞은 쪽을 돌려준다. XAML 에선
    /// <c>{x:Static brand:BrandStrings.AppTitle}</c> 형태로 정적 참조 가능.
    /// </summary>
    public static class BrandStrings
    {
        private const string TableClothEntryAssemblyName = "TableCloth";

        private static readonly bool _isTableClothEntry = ResolveIsTableClothEntry();

        private static bool ResolveIsTableClothEntry()
        {
            try
            {
                var entry = Assembly.GetEntryAssembly()?.GetName().Name;
                return string.Equals(entry, TableClothEntryAssemblyName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // 진입 어셈블리 조회 실패 시(드물지만 호스팅 모드에 따라) 안전한 기본은 Spork 브랜드.
                return false;
            }
        }

        /// <summary>
        /// 디버깅/로깅 등에서 사용할 entry 식별. 단독 테스트가 필요한 경우 외엔 호출하지 않아도 됨.
        /// </summary>
        public static bool IsTableClothEntry => _isTableClothEntry;

        public static string AppTitle => _isTableClothEntry
            ? UIStringResources.TableCloth_SandboxAppTitle
            : UIStringResources.Spork_AppTitle;

        public static string DryRunInstructionMessage => _isTableClothEntry
            ? UIStringResources.TableCloth_DryRunInstructionMessage
            : UIStringResources.Spork_DryRunInstructionMessage;

        public static string ShortcutLinkName => _isTableClothEntry
            ? UIStringResources.TableCloth_ShortcutLinkName
            : UIStringResources.Spork_ShortcutLinkName;

        public static string ShortcutDescription => _isTableClothEntry
            ? UIStringResources.TableCloth_ShortcutDescription
            : UIStringResources.Spork_ShortcutDescription;

        /// <summary>
        /// 바탕화면 바로가기에 전달할 커맨드라인 인자.
        /// TableCloth.exe 가 entry 면 verb 디스패처를 거치므로 <c>spork</c> 토큰이 반드시 필요하다.
        /// 빠뜨리면 바로가기가 호스트 런처 모드로 빠져 sandbox 안 흐름이 깨진다.
        /// Spork.exe 단독 entry 라면 자체가 Spork 진입점이라 인자가 필요 없다.
        /// </summary>
        public static string ShortcutArguments => _isTableClothEntry
            ? "spork"
            : string.Empty;

        public static string TitleText_Error => _isTableClothEntry
            ? UIStringResources.TitleText_Error
            : UIStringResources.Spork_TitleText_Error;

        public static string TitleText_Info => _isTableClothEntry
            ? UIStringResources.TitleText_Info
            : UIStringResources.Spork_TitleText_Info;

        public static string TitleText_Question => _isTableClothEntry
            ? UIStringResources.TitleText_Question
            : UIStringResources.Spork_TitleText_Question;

        public static string TitleText_Warning => _isTableClothEntry
            ? UIStringResources.TitleText_Warning
            : UIStringResources.Spork_TitleText_Warning;

        public static string Error_Already_Running => _isTableClothEntry
            ? ErrorStrings.Error_Already_TableCloth_Running
            : ErrorStrings.Error_Already_Spork_Running;
    }
}
