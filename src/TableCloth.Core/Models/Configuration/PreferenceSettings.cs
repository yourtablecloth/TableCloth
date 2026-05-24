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
        /// 모두의 프린터 설치를 샌드박스 시작 직후 실행할지 여부를 나타냅니다.
        /// </summary>
        public bool InstallEveryonesPrinter { get; set; } = true;

        /// <summary>
        /// Adobe Reader 설치를 샌드박스 시작 직후 실행할지 여부를 나타냅니다.
        /// </summary>
        public bool InstallAdobeReader { get; set; } = true;

        /// <summary>
        /// 한컴오피스 뷰어 설치를 샌드박스 시작 직후 실행할지 여부를 나타냅니다.
        /// </summary>
        public bool InstallHancomOfficeViewer { get; set; } = true;

        /// <summary>
        /// RaiDrive 설치를 샌드박스 시작 직후 실행할지 여부를 나타냅니다.
        /// </summary>
        public bool InstallRaiDrive { get; set; } = true;

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
        public string? DataDirectoryHostPath { get; set; } = null;

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
        public string? SandboxFolder { get; set; } = null;

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
