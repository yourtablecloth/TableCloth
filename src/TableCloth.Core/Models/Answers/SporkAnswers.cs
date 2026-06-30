namespace TableCloth.Models.Answers
{
    /// <summary>
    /// 호스트(TableCloth)가 샌드박스 staging의 App 폴더에 떨궈두는 진입 파라미터.
    /// 샌드박스 안에서 Spork 진입 흐름이 부팅 1회 초기화(컬처 적용, DNS 설정, 인증서 배치,
    /// NPKI 복사 등) 시 이 파일을 읽어 사용한다.
    /// </summary>
    public sealed class SporkAnswers
    {
        public string HostUILocale { get; set; } = null;

        /// <summary>
        /// 호스트의 라이트/다크 테마 선호(<c>AppsUseLightTheme</c>). <see langword="true"/>=라이트,
        /// <see langword="false"/>=다크, <see langword="null"/>=알 수 없음(이 경우 샌드박스 테마를 건드리지 않음).
        /// 샌드박스 부팅 시 SandboxBootstrap이 이 값으로 게스트의 라이트/다크 테마를 시작 시점에 맞춘다.
        /// RDP 프로토콜에는 호스트 테마 전이를 세션으로 전달할 채널이 없어 "시작 시점 일치"까지만 가능하다(이슈 #246).
        /// </summary>
        public bool? HostUsesLightTheme { get; set; } = null;

        /// <summary>
        /// 호스트가 인증서 쌍을 전달했는지 여부. <see langword="true"/>이면 staging의
        /// <c>App\certs\signCert.der</c>·<c>signPri.key</c>가 함께 떨어져 있고, 샌드박스 진입 시
        /// 아래 cert 관련 필드를 사용해 NPKI 표준 경로로 배치한다.
        /// </summary>
        public bool HasCertPair { get; set; }

        /// <summary>
        /// 인증서 Subject의 O(조직) 값. NPKI 디렉터리 트리 구성에 사용된다.
        /// </summary>
        public string CertOrganization { get; set; } = null;

        /// <summary>
        /// 개인용 NPKI 인증서 여부(NonRepudiation + DigitalSignature). <see langword="true"/>이면
        /// <c>USER\&lt;subject&gt;</c> 하위로 배치, 아니면 조직 폴더 바로 아래로 배치한다.
        /// </summary>
        public bool CertIsPersonalCert { get; set; }

        /// <summary>
        /// 개인용 인증서 NPKI 폴더 leaf로 사용되는 subject DN. <see cref="CertIsPersonalCert"/>가
        /// <see langword="true"/>일 때만 의미가 있다.
        /// </summary>
        public string CertSubjectNameForNpkiApp { get; set; } = null;
    }
}
