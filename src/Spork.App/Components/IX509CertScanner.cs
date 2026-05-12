using System.Collections.Generic;
using TableCloth.Models.Configuration;

namespace Spork.Components
{
    /// <summary>
    /// 샌드박스 내부의 NPKI 폴더에서 공동인증서 페어(signCert.der + signPri.key)를 스캔합니다.
    /// </summary>
    public interface IX509CertScanner
    {
        /// <summary>
        /// 샌드박스의 표준 NPKI 위치(<see cref="TableCloth.Models.WindowsSandbox.SandboxMountPaths.NpkiCanonicalPath"/>)
        /// 아래의 모든 인증서 페어를 재귀로 찾아 반환합니다. 폴더가 없으면 빈 시퀀스.
        /// </summary>
        IEnumerable<X509CertPair> ScanSandboxNpkiCertificates();
    }
}
