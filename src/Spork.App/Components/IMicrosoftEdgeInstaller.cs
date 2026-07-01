using System;
using System.Threading;
using System.Threading.Tasks;

namespace Spork.Components
{
    /// <summary>
    /// 드물게 Windows Sandbox 기본 이미지에 Microsoft Edge(msedge.exe)가 없는 경우를 대비한
    /// 수동 설치/복구 서비스(이슈 #184). 자동 스텝으로 상시 실행하면 정상 샌드박스에서도
    /// 대용량 MSI 를 늘 내려받게 되므로, 사용자가 필요할 때 명시적으로 호출하는 형태로 분리했다.
    /// </summary>
    public interface IMicrosoftEdgeInstaller
    {
        /// <summary>Microsoft Edge(msedge.exe)가 설치되어 있는지 확인한다.</summary>
        bool IsEdgeInstalled();

        /// <summary>
        /// 현재 아키텍처의 Microsoft Edge Enterprise MSI 를 내려받아 무인 설치한다. WebView2 런타임은
        /// 브라우저(msedge.exe)를 복원하지 못하므로 사용하지 않고 반드시 전체 Edge 를 설치한다.
        /// 설치 후에도 Edge 가 확인되면 <see langword="true"/>, 위치 조회/다운로드/설치 중 하나라도
        /// 실패하면 <see langword="false"/>를 반환한다(예외는 삼키지 않고 그대로 전파한다).
        /// </summary>
        /// <param name="progress">다운로드 진행률(0.0~1.0)을 보고받을 콜백. 선택 사항.</param>
        Task<bool> InstallOrRepairAsync(IProgress<double> progress = null, CancellationToken cancellationToken = default);
    }
}
