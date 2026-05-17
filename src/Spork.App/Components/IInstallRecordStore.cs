using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.UserData;

namespace Spork.Components
{
    /// <summary>
    /// 실제 머신의 설치 상태 fingerprint 를 추적하는 저장소. 사용자 선호(<see cref="IUserDataStore"/>)와 분리된
    /// 별도 파일(<c>install-state.json</c>)을 항상 <c>%LocalAppData%\Spork</c> 아래 둔다.
    /// </summary>
    /// <remarks>
    /// 같은 경로 표현이 두 실행 환경에서 의도한 의미를 자동으로 갖는다:
    /// <list type="bullet">
    ///   <item>Windows Sandbox: WDAGUtilityAccount LocalAppData 가 sandbox VHD 안이라 세션 종료 시
    ///         설치 기록도 함께 사라진다. 새 sandbox 부팅 = 깨끗한 상태 = 모든 패키지 재설치 필요.</item>
    ///   <item>단독 Spork.exe(비-샌드박스): 사용자 머신의 LocalAppData 라 세션 간 영속.</item>
    /// </list>
    /// <para>
    /// 즐겨찾기 같은 사용자 선호와 분리된 이유: 사용자 선호는 sandbox 에서 mounted Desktop\Data 로
    /// 호스트와 공유되어 세션 간 보존되지만, 설치 기록은 호스트와 sandbox 가 별개 머신이라 공유돼선 안 된다.
    /// 한쪽이 설치했다고 다른 쪽이 설치된 것은 아님.
    /// </para>
    /// </remarks>
    public interface IInstallRecordStore
    {
        /// <summary>설치 기록 파일의 절대 경로.</summary>
        string FilePath { get; }

        /// <summary>
        /// 메모리에 캐시된 설치 기록. <see cref="EnsureLoadedAsync"/> 호출 전엔 빈 객체.
        /// </summary>
        InstallRecord Current { get; }

        /// <summary>디스크에서 1회 로드. 이미 로드되어 있으면 no-op.</summary>
        Task EnsureLoadedAsync(CancellationToken cancellationToken = default);

        /// <summary>250ms 디바운스 저장. 빠른 연속 변경을 1회 디스크 쓰기로 합친다.</summary>
        void ScheduleSave();

        /// <summary>디바운스 무시하고 즉시 저장.</summary>
        Task FlushAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// fingerprint 가 기록되어 있는지 thread-safe 하게 확인.
        /// </summary>
        bool IsInstalled(string fingerprint);

        /// <summary>
        /// fingerprint 를 기록에 추가하고 디바운스 저장을 예약. 새 항목 추가 시에만 저장 트리거.
        /// </summary>
        void AddInstalledFingerprint(string fingerprint);

        /// <summary>
        /// 현재 카탈로그에 더 이상 존재하지 않는 fingerprint 를 제거한다. 카탈로그 진입 시 1회 호출해
        /// 무한 누적 방지.
        /// </summary>
        void PruneStaleFingerprints(IEnumerable<string> activeFingerprints);
    }
}
