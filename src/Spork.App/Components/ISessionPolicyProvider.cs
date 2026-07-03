namespace Spork.Components
{
    /// <summary>
    /// [미리 보기] 샌드박스 세션에 적용할 정책을 공급하는 seam. 현재는 유휴 자동 종료 정책 하나만 노출한다(이슈 #197).
    /// 무료판은 호스트가 <c>SporkAnswers</c>로 전달한 값을 그대로 사용하지만, 이 인터페이스를 경계로 두면
    /// 나중에 SMB(강제·중앙관리·감사) 계층이 사용자가 끄거나 늘릴 수 없는 잠긴/관리형 정책을 공급하도록
    /// 재작업 없이 교체할 수 있다. 즉 이 기능이 무료/유료 경계를 검증하는 파일럿이 된다.
    /// </summary>
    public interface ISessionPolicyProvider
    {
        /// <summary>현재 세션의 유휴 자동 종료 정책을 반환한다. 비활성이면 <see cref="IdleAutoLogoutPolicy.Disabled"/>.</summary>
        IdleAutoLogoutPolicy GetIdleAutoLogoutPolicy();
    }

    /// <summary>유휴 자동 종료 정책. <see cref="Enabled"/>가 false면 모니터가 동작하지 않는다.</summary>
    public sealed record IdleAutoLogoutPolicy(bool Enabled, int TimeoutMinutes)
    {
        public static readonly IdleAutoLogoutPolicy Disabled = new(false, 0);
    }
}
