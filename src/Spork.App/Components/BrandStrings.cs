using System;
using System.Reflection;
using TableCloth.Resources;

namespace Spork.Components
{
    /// <summary>
    /// Spork.App UI 가 사용자에게 보여줄 브랜드 표기.
    /// 프로그램 구조가 단일 바이너리(+단독 Spork)로 통합되면서 옛 '포카락' 브랜드는 폐기했고,
    /// 두 진입점(<c>TableCloth.exe spork</c> / 단독 <c>Spork.exe</c>) 모두 <b>식탁보</b> 브랜드로 통일한다.
    /// XAML 에선 <c>{x:Static brand:BrandStrings.AppTitle}</c> 형태로 정적 참조 가능.
    /// </summary>
    /// <remarks>
    /// entry 어셈블리 판정(<see cref="IsTableClothEntry"/>)은 표시 브랜드가 아니라 데스크톱 바로가기의
    /// 커맨드라인 인자(<see cref="ShortcutArguments"/>)를 결정하는 <b>기능적</b> 목적으로만 남는다.
    /// </remarks>
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
                // 진입 어셈블리 조회 실패 시(드물지만 호스팅 모드에 따라)엔 단독 Spork.exe 로 간주.
                return false;
            }
        }

        /// <summary>
        /// entry 가 통합 <c>TableCloth.exe</c> 인지 여부. 바로가기 인자 결정에만 쓰인다(표시 브랜드 아님).
        /// </summary>
        public static bool IsTableClothEntry => _isTableClothEntry;

        // ── 표시 브랜드: 두 진입점 모두 식탁보로 통일 ──────────────────────────────
        public static string AppTitle => UIStringResources.TableCloth_SandboxAppTitle;

        public static string DryRunInstructionMessage => UIStringResources.TableCloth_DryRunInstructionMessage;

        public static string ShortcutLinkName => UIStringResources.TableCloth_ShortcutLinkName;

        public static string ShortcutDescription => UIStringResources.TableCloth_ShortcutDescription;

        public static string TitleText_Error => UIStringResources.TitleText_Error;

        public static string TitleText_Info => UIStringResources.TitleText_Info;

        public static string TitleText_Question => UIStringResources.TitleText_Question;

        public static string TitleText_Warning => UIStringResources.TitleText_Warning;

        public static string Error_Already_Running => ErrorStrings.Error_Already_TableCloth_Running;

        /// <summary>
        /// 바탕화면 바로가기에 전달할 커맨드라인 인자(기능적 — 표시 브랜드와 무관).
        /// 통합 <c>TableCloth.exe</c> 가 entry 면 verb 디스패처를 거치므로 <c>spork</c> 토큰이 반드시 필요하다.
        /// 빠뜨리면 바로가기가 호스트 런처 모드로 빠져 sandbox 안 흐름이 깨진다.
        /// 단독 <c>Spork.exe</c> entry 라면 자체가 Spork 진입점이라 인자가 필요 없다.
        /// </summary>
        public static string ShortcutArguments => _isTableClothEntry
            ? "spork"
            : string.Empty;
    }
}
