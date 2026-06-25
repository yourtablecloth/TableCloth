using System.Collections.Generic;
using TableCloth.Models.Configuration;

namespace Spork.Components
{
    /// <summary>
    /// 현재 환경(WSB 또는 사용자가 직접 만든 VM)의 NPKI 위치들에서 공동인증서 페어
    /// (signCert.der + signPri.key)를 스캔합니다.
    /// </summary>
    public interface IX509CertScanner
    {
        /// <summary>
        /// 후보 NPKI 루트들(실제 LocalLow\NPKI / WSB canonical / Desktop\NPKI 마운트 / 제거식 드라이브)
        /// 아래의 모든 인증서 페어를 재귀로 찾아 CertHash 중복 제거 후 반환합니다. 없으면 빈 시퀀스.
        /// </summary>
        /// <remarks>
        /// WSB 안에서는 사용자 계정이 WDAGUtilityAccount 라 실제 LocalLow 가 WSB canonical 과 동일
        /// 경로로 풀리므로 모드 1(식탁보 + WSB) 동작이 그대로 유지됩니다. 사용자 VM(임의 계정명)이나
        /// 무설치 WSB(호스트 NPKI RO 마운트)도 같은 한 메서드로 처리됩니다.
        /// </remarks>
        IEnumerable<X509CertPair> ScanLocalNpkiCertificates();
    }
}
