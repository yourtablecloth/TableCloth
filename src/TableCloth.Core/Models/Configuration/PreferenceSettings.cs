using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TableCloth.Models.Configuration
{
    /// <summary>
    /// 식탁보의 설정을 나타냅니다.
    /// </summary>
    public class PreferenceSettings
    {
        /// <summary>
        /// Disclaimer 알림 주기 (일 단위)
        /// </summary>
        public const double DisclaimerNotificationIntervalDays = 7d;

        /// <summary>
        /// 호스트의 오디오 입력을 샌드박스 안으로 전달할지 여부를 나타냅니다.
        /// </summary>
        public bool UseAudioRedirection { get; set; } = false;

        /// <summary>
        /// 호스트의 비디오 입력을 샌드박스 안으로 전달할지 여부를 나타냅니다.
        /// </summary>
        public bool UseVideoRedirection { get; set; } = false;

        /// <summary>
        /// 샌드박스 안의 프린터 출력을 호스트의 프린터로 전달할지 여부를 나타냅니다.
        /// </summary>
        public bool UsePrinterRedirection { get; set; } = false;

        /// <summary>
        /// 로그 수집 기능을 사용할지 여부를 나타냅니다.
        /// </summary>
        public bool UseLogCollection { get; set; } = true;

        /// <summary>
        /// 식탁보 사용 시 주의 사항 동의 여부를 언제 확인했는지 시점을 기록합니다.
        /// </summary>
        public DateTime? LastDisclaimerAgreedTime { get; set; } = null;

        /// <summary>
        /// 즐겨찾기만 표시할지 여부를 기록합니다.
        /// </summary>
        public bool ShowFavoritesOnly { get; set; } = false;

        /// <summary>
        /// 즐겨찾기로 등록된 서비스 아이디를 기록합니다.
        /// </summary>
        public List<string> Favorites { get; set; } = new List<string>();

        /// <summary>
        /// 마지막으로 사용한 공동 인증서 해시 값을 기록합니다.
        /// </summary>
        public string LastUsedCertHash { get; set; } = null;

        /// <summary>
        /// 라이선스 동의 시점을 기록합니다.
        /// </summary>
        public DateTime? LicenseAgreedTime { get; set; } = null;

        /// <summary>
        /// 라이선스 동의 시 프로그램 버전을 기록합니다.
        /// </summary>
        public string LicenseAgreedVersion { get; set; } = null;

        /// <summary>
        /// 샌드박스에 매핑할 사용자 지정 폴더 목록입니다.
        /// </summary>
        public List<MappedFolderSetting> MappedFolders { get; set; } = new List<MappedFolderSetting>();

        /// <summary>
        /// 영속 상태(즐겨찾기, 사용 기록, 사용자 백업)를 저장하는 Data 디렉터리의 호스트 경로입니다.
        /// 샌드박스 시작 시 항상 읽기-쓰기로 마운트됩니다.
        /// null 또는 빈 문자열이면 호스트의 기본 Data 경로를 사용합니다.
        /// </summary>
        public string DataDirectoryHostPath { get; set; } = null;

        /// <summary>
        /// 호스트의 공동인증서 폴더(<c>%USERPROFILE%\AppData\LocalLow\NPKI</c>)를 샌드박스로 공유할지 여부입니다.
        /// true일 때만 시작 시 NPKI 폴더가 마운트되며, 호스트에 폴더 자체가 없으면 자동으로 건너뛰어집니다.
        /// 기본값은 true (인터넷 뱅킹/공공 사이트 사용을 위한 핵심 동작이므로).
        /// </summary>
        public bool ShareNpkiFolder { get; set; } = true;

        /// <summary>
        /// 샌드박스에서 GPU 가속(vGPU + Edge 하드웨어 가속)을 사용할지 여부.
        /// 기본값은 false — 즉 호환성·안정성을 우선시하여 GPU를 끄고 소프트웨어 렌더링으로 동작합니다.
        /// false일 때: WSB가 <c>&lt;vGPU&gt;Disable&lt;/vGPU&gt;</c>로 호스트 GPU 공유를 막고,
        /// StartupScript가 Edge 정책(<c>HardwareAccelerationModeEnabled=0</c>)을 적용해 Edge가
        /// 소프트웨어 렌더링을 강제하도록 합니다. 일부 GPU 환경(특정 NVIDIA 드라이버 등)에서
        /// 보고된 Edge 흰 화면 증상이나 vGPU 관련 비결정적 동작을 회피하기 위한 기본 동작입니다.
        /// true로 켜면 호스트 GPU를 샌드박스와 공유(<c>&lt;vGPU&gt;Default&lt;/vGPU&gt;</c>)하고 Edge가
        /// 정상적으로 GPU 가속을 사용합니다. 가속이 필요한 시각화 작업·미디어 재생 등에서만 켜는 것을 권장합니다.
        /// </summary>
        public bool EnableSandboxGpuAcceleration { get; set; } = false;

        /// <summary>
        /// 샌드박스 부팅 시 게스트의 DNS 이름 해석이 실패하면 공용 DNS(8.8.8.8/1.1.1.1)로 폴백할지 여부입니다.
        /// 기본값은 true. true여도 <b>기존 DNS로 해석이 되면 건드리지 않고</b>, 해석이 실패할 때만 폴백합니다
        /// (probe-then-fallback). 이렇게 하면 게스트에 DNS가 안 잡히는 환경은 자동 구제되면서도, 사내/관리형
        /// 네트워크의 정상 DNS(내부 리졸버)는 덮어쓰지 않습니다. 공용 DNS가 정책상 막혔거나 어떤 DNS 수정도
        /// 원치 않는 엄격한 환경이라면 false로 끕니다(이슈 #285).
        /// </summary>
        public bool EnableSandboxPublicDnsFallback { get; set; } = true;

        /// <summary>
        /// 호스트에 설치된 ZScaler 루트 인증서를 샌드박스 안으로 전파할지 여부입니다.
        /// ZScaler 같은 엔드포인트 통제(SSL 검사) 환경에서는 호스트가 신뢰하는 ZScaler 루트 인증서가
        /// 샌드박스로 자동 전파되지 않아 샌드박스 내부의 HTTPS 연결이 모두 차단됩니다. 이 옵션을 켜면
        /// 호스트의 <c>LocalMachine\Root</c> 저장소에서 Subject에 "Zscaler"가 포함된 루트 인증서를 찾아
        /// 샌드박스로 전달하고, 샌드박스 부팅 시 사용자 신뢰 루트 저장소에 등록하여 인터넷 연결을 복원합니다
        /// (이슈 #292). 기본값은 꺼짐이며, 호스트에 해당 인증서가 없으면 켜져 있어도 자동으로 건너뜁니다.
        /// <b>이 옵션은 무설치(포터블) 식탁보에서는 지원되지 않습니다.</b>
        /// </summary>
        public bool EnableZScalerRootCertPropagation { get; set; } = false;

        /// <summary>
        /// [미리 보기] 샌드박스에 일정 시간 키보드·마우스 입력이 없으면 자동으로 종료할지 여부입니다.
        /// 인증된 뱅킹 세션을 자리 비운 사이 열어두는 위험을 줄이기 위한 실험적 보안 기능이며, 기본값은 꺼짐입니다.
        /// </summary>
        public bool EnableIdleAutoLogout { get; set; } = false;

        /// <summary>
        /// [미리 보기] 자동 종료를 실행하기까지의 유휴 허용 시간(분)입니다. <see cref="EnableIdleAutoLogout"/>가
        /// 켜져 있을 때만 의미가 있으며, 기본값은 10분입니다.
        /// </summary>
        public int IdleAutoLogoutMinutes { get; set; } = 10;

        /// <summary>
        /// 업데이트를 받아올 릴리스 링(채널). 기본값은 <see cref="ReleaseChannel.Retail"/>(안정).
        /// <see cref="ReleaseChannel.Preview"/>로 바꾸면 선행 검증용 프리릴리스(미리 보기)를 받습니다(이슈 #296).
        /// JSON 에는 문자열("Retail"/"Preview")로 저장된다(TableClothJsonContext 의 UseStringEnumConverter).
        /// Retail(1.20.x)이 쓴 설정과 형식이 동일해 미리 보기로 전환한 사용자의 설정이 그대로 이어진다.
        /// </summary>
        public ReleaseChannel UpdateChannel { get; set; } = ReleaseChannel.Retail;

        /// <summary>
        /// Disclaimer 알림을 표시해야 하는지 여부를 반환합니다.
        /// </summary>
        /// <param name="currentTime">현재 시간 (UTC)</param>
        /// <returns>Disclaimer 알림을 표시해야 하면 true, 그렇지 않으면 false</returns>
        public bool ShouldNotifyDisclaimer(DateTime currentTime)
        {
            if (!LastDisclaimerAgreedTime.HasValue)
                return true;

            if ((currentTime - LastDisclaimerAgreedTime.Value).TotalDays >= DisclaimerNotificationIntervalDays)
                return true;

            return false;
        }

        /// <summary>
        /// 현재 시간 기준으로 Disclaimer 알림을 표시해야 하는지 여부를 반환합니다.
        /// </summary>
        public bool ShouldNotifyDisclaimer()
            => ShouldNotifyDisclaimer(DateTime.UtcNow);
    }

    /// <summary>
    /// 사용자 지정 매핑 폴더 설정을 나타냅니다.
    /// </summary>
    public class MappedFolderSetting
    {
        /// <summary>
        /// 호스트 시스템의 폴더 경로입니다.
        /// </summary>
        public string HostFolder { get; set; } = string.Empty;

        /// <summary>
        /// 샌드박스 내의 폴더 경로입니다. (선택사항)
        /// </summary>
        public string SandboxFolder { get; set; } = null;

        /// <summary>
        /// 읽기 전용 여부입니다.
        /// </summary>
        public bool ReadOnly { get; set; } = true;

        /// <summary>
        /// 샌드박스 시작 직전 검증에서 호스트 폴더가 존재/접근 가능하지 않은 것으로 판정된 경우 true.
        /// 런타임 상태이며 환경 설정 파일에 직렬화되지 않습니다 (다음 실행에 재검증).
        /// 마운트는 조용히 건너뛰고, 리스트 UI에서만 "사용 불가" 마킹으로 사용자에게 알립니다.
        /// </summary>
        [JsonIgnore]
        public bool IsUnavailable { get; set; }
    }
}
