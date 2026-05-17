using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models.UserData;

namespace Spork.Components
{
    /// <summary>
    /// 사용자 선호 데이터(즐겨찾기, 최근 사용 기록 등)를 메모리 캐시 + 디바운스 저장 모델로 영속화한다.
    /// 설치 상태 fingerprint 는 환경(호스트 vs sandbox)에 종속이라 본 저장소와 분리된
    /// <see cref="IInstallRecordStore"/>가 별도 관리한다.
    /// </summary>
    /// <remarks>
    /// 저장 경로:
    /// <list type="bullet">
    ///   <item>Windows Sandbox 안: <c>C:\Users\WDAGUtilityAccount\Desktop\Data\user-data.json</c>
    ///         (호스트가 마운트한 Data 폴더 → 세션 간 영속).</item>
    ///   <item>비-샌드박스 환경(단독 Spork.exe 등): <c>%LocalAppData%\Spork\user-data.json</c>.</item>
    /// </list>
    /// </remarks>
    public interface IUserDataStore
    {
        /// <summary>현재 사용자 데이터 파일의 절대 경로.</summary>
        string UserDataFilePath { get; }

        /// <summary>
        /// 메모리에 캐시된 사용자 데이터 인스턴스. <see cref="EnsureLoadedAsync"/> 호출 전엔 빈 객체.
        /// 모든 소비자가 같은 참조를 공유하므로 다른 컴포넌트의 변형이 즉시 보인다.
        /// </summary>
        SporkUserData Current { get; }

        /// <summary>
        /// 디스크에서 데이터를 1회 로드해 <see cref="Current"/>에 채운다. 이미 로드되어 있으면 no-op.
        /// 동시에 여러 호출자가 들어와도 안전하게 한 번만 로드한다.
        /// </summary>
        Task EnsureLoadedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 디바운스 저장 예약. 250ms 안에 다시 호출되면 이전 보류분은 취소되고 타이머가 재시작된다.
        /// 빠른 연속 변경(즐겨찾기 연타 등)이 1회 디스크 쓰기로 합쳐진다.
        /// </summary>
        void ScheduleSave();

        /// <summary>
        /// 보류 중인 디바운스를 무시하고 즉시 저장. 종료 시점이나 critical 시점에 사용.
        /// </summary>
        Task FlushAsync(CancellationToken cancellationToken = default);
    }
}
