using System.Collections.Generic;

namespace TableCloth.Models.UserData
{
    /// <summary>
    /// 실제 머신에 설치된 패키지/Edge 확장/CustomBootstrap 스크립트의 fingerprint 기록.
    /// 본 데이터는 사용자 선호(Favorites 등)와 달리 환경에 묶이는 정보이므로 별도 파일
    /// (<c>install-state.json</c>)에 분리 저장된다.
    /// </summary>
    /// <remarks>
    /// 저장 위치:
    /// <list type="bullet">
    ///   <item>Windows Sandbox 안: WDAGUtilityAccount 의 <c>%LocalAppData%\Spork\install-state.json</c>
    ///         — sandbox VHD 안이라 세션 종료 시 사라진다. 새 sandbox 부팅 = 깨끗한 상태.</item>
    ///   <item>비-샌드박스 환경(단독 Spork.exe 등): 사용자 PC 의 <c>%LocalAppData%\Spork\install-state.json</c>
    ///         — 실제 머신 상태와 일치하여 세션 간 영속.</item>
    /// </list>
    /// 같은 표현(<c>%LocalAppData%</c>)이 두 환경에서 자연스럽게 올바른 의미를 가지므로 추가 분기 없이
    /// 동일 코드가 양쪽에서 동작한다.
    /// </remarks>
    public sealed class InstallRecord
    {
        /// <summary>
        /// Data 디렉터리 안에 저장되는 파일 이름.
        /// </summary>
        public const string FileName = "install-state.json";

        /// <summary>
        /// 현재 스키마 버전. 향후 마이그레이션 분기용.
        /// </summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>
        /// 설치 완료된 항목의 fingerprint 집합. 포맷은 <see cref="PackageFingerprints"/> 헬퍼가 결정한다
        /// (예: <c>pkg:&lt;sha256(url|args)&gt;</c>, <c>edgeext:&lt;id&gt;</c>, <c>ps:&lt;sha256&gt;</c>).
        /// </summary>
        public HashSet<string> InstalledFingerprints { get; set; } = new HashSet<string>();
    }
}
