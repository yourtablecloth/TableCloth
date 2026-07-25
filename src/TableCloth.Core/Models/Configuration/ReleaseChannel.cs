namespace TableCloth.Models.Configuration
{
    /// <summary>
    /// 앱이 업데이트를 받아오는 릴리스 링(채널)을 나타냅니다.
    /// </summary>
    /// <remarks>
    /// 이슈 #296: WPF→Avalonia+Native AOT 전환처럼 변화 폭이 큰 릴리스를 안정 사용자와 분리해 조기 검증하기 위해
    /// Retail(안정)/Preview(미리 보기) 두 링을 둔다. 사용자는 옵션 창에서 선택하며, 선택값에 따라
    /// <see cref="TableCloth.Components.IAppUpdateManager"/>가 Velopack 채널·GitHub 프리릴리스 조회를 분기한다.
    /// 설계: docs/RELEASE_CHANNELS.md.
    /// </remarks>
    public enum ReleaseChannel
    {
        /// <summary>안정 채널(대다수 사용자, 정식 릴리스).</summary>
        Retail = 0,

        /// <summary>미리 보기 채널(opt-in, 프리릴리스 — 선행 검증용).</summary>
        Preview = 1,
    }
}
