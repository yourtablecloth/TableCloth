namespace Spork.Components
{
    /// <summary>
    /// [미리 보기] 샌드박스 세션의 유휴 시간을 감시해, 정책상 허용 시간을 넘기면 자동으로 종료하는 모니터(이슈 #197).
    /// 정책이 비활성이면 <see cref="Start"/>는 아무 일도 하지 않는다.
    /// </summary>
    public interface ISessionIdleMonitor
    {
        /// <summary>
        /// 정책이 활성일 때만 유휴 감시를 시작한다. 시작했으면 <see langword="true"/>, 정책이 꺼져 있거나
        /// WPF 애플리케이션이 준비되지 않아 시작하지 못했으면 <see langword="false"/>를 반환한다(가드가 이 값을
        /// 보고 할 일이 없으면 종료한다). WPF 애플리케이션이 떠 있는 상태(예: idle-guard 진입점 Startup)에서 호출한다.
        /// </summary>
        bool Start();
    }
}
