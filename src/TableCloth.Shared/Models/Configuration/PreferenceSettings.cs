using System;
using System.Collections.Generic;

namespace TableCloth.Models.Configuration
{
    /// <summary>
    /// 식탁보의 설정을 나타냅니다.
    /// </summary>
    public class PreferenceSettings
    {
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
        /// Internet Explorer 호환성 모드를 사용할지 여부를 나타냅니다.
        /// </summary>
        public bool EnableInternetExplorerMode { get; set; } = true;

        /// <summary>
        /// 로그 수집 기능을 사용할지 여부를 나타냅니다.
        /// </summary>
        public bool UseLogCollection { get; set; } = true;

        /// <summary>
        /// 식탁보 사용 시 주의 사항 동의 여부를 언제 확인했는지 시점을 기록합니다.
        /// </summary>
        public DateTime? LastDisclaimerAgreedTime { get; set; } = null;

        /// <summary>
        /// 새 버전 UI를 활성화할지 여부를 기록합니다.
        /// </summary>
        public bool V2UIOptIn { get; set; } = true;

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
        public string
#if !NETFX
            ?
#endif
            LastUsedCertHash { get; set; } = null;
    }
}
