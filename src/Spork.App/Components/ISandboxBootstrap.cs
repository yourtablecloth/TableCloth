using System.Threading;
using System.Threading.Tasks;

namespace Spork.Components
{
    /// <summary>
    /// 샌드박스 진입 직후 한 번만 실행해야 하는 부팅 초기화 작업의 모음 — DNS 서버 강제 설정,
    /// 호스트가 떨어뜨려 둔 인증서 쌍의 NPKI 표준 경로 배치, RO 마운트된 NPKI를 쓰기 가능 사본으로
    /// 복제하는 작업 등을 담는다. 이전에는 StartupScript.cmd가 PowerShell + 다수의 CMD 라인으로
    /// 처리하던 로직을 .NET 측으로 옮긴 것이다 — PowerShell 콜드 스타트(수 초)를 제거해
    /// Spork UI 진입을 가속한다.
    /// </summary>
    public interface ISandboxBootstrap
    {
        /// <summary>
        /// 호스트가 사전 합의된 staging 경로에 떨궈둔 자료(<c>App\certs\*</c>, <c>SporkAnswers.json</c>)와
        /// 표준 마운트 경로(<c>Desktop\NPKI</c>)를 기반으로 부팅 초기화를 수행한다. 모든 단계는
        /// best-effort — 실패가 catalog 진입을 막지 않는다. 샌드박스가 아닌 환경에서도 안전하게
        /// no-op이 되도록 조건 분기한다.
        /// </summary>
        Task RunAsync(CancellationToken cancellationToken = default);
    }
}
