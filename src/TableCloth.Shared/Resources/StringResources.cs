using System;
using System.Collections.Generic;
using System.Linq;
using TableCloth.Models.Catalog;

namespace TableCloth.Resources
{
    // 공통 문자열들
    internal static partial class StringResources
    {
        internal static readonly string AppName = "식탁보";

        internal static readonly string AppCopyright = "(c) 2021 남정현";

        internal static readonly string OkayButtonText = "확인";

        internal static readonly string CancelButtonText = "취소";

        internal static readonly string CatalogUrl =
            "https://dotnetdev-kr.github.io/TableCloth/Catalog.xml";

        internal static readonly string UserAgentText =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.164 Safari/537.36";
    }
#pragma warning disable IDE0040, IDE0066

    // 공통 대화 상자 제목들
    partial class StringResources
    {
        internal static readonly string TitleText_Info
            = $"{AppName} 정보";

        internal static readonly string TitleText_Error
            = $"{AppName} 오류";

        internal static readonly string TitleText_Warning
            = $"{AppName} 경고";
    }

    // 비 사용자 인터페이스 문자열들
    partial class StringResources
    {
        internal static readonly string InternetService_UnknownText = "알 수 없음";

        internal static readonly string SandboxWorkingDir_ReadmeText = @"식탁보 사용 완료 후 정보 보호를 위한 권장 사항

식탁보는 편의를 위하여 사용자의 인증서 파일을 샌드박스에 전송할 수 있도록 별도의 작업 디렉터리에 파일을 복사합니다.
하지만 샌드박스 프로그램이 언제 종료될지 식탁보 프로그램에서는 파악이 불가능합니다.

따라서 샌드박스 사용을 마친 후에는 bwsb 폴더를 모두 지우시는 것을 권장합니다.";

        internal static string InternetService_DisplayText(CatalogInternetService svc)
        {
            var defaultString = $"{svc.DisplayName} - {svc.Url}";
            var pkgs = svc.Packages;

            var hasCompatNotes = !string.IsNullOrWhiteSpace(svc.CompatibilityNotes);

            if (hasCompatNotes)
                defaultString = $"*{defaultString}";

            if (pkgs != null && pkgs.Count > 0)
                defaultString = $"{defaultString} (총 {pkgs.Count}개 프로그램 설치)";

            return defaultString;
        }

        internal static string InternetServiceCategory_DisplayText(CatalogInternetServiceCategory value)
        {
            switch (value)
            {
                case CatalogInternetServiceCategory.Banking:
                    return "뱅킹";
                case CatalogInternetServiceCategory.CreditCard:
                    return "신용 카드";
                case CatalogInternetServiceCategory.Education:
                    return "교육";
                case CatalogInternetServiceCategory.Financing:
                    return "대출/금융";
                case CatalogInternetServiceCategory.Government:
                    return "공공";
                case CatalogInternetServiceCategory.Security:
                    return "증권/투자";
                case CatalogInternetServiceCategory.Insurance:
                    return "보험";
                case CatalogInternetServiceCategory.Other:
                default:
                    return "기타";
            }
        }
    }

    // 메인 화면에 표시될 문자열들
    partial class StringResources
    {
        internal static readonly string MainForm_Title
            = $"{AppName} - 컴퓨터를 깨끗하게 사용하세요!";

        internal static readonly string MainForm_SelectOptionsLabelText
            = @"원하는 옵션을 선택해주세요.";

        internal static readonly string MainForm_MapNpkiCertButtonText
            = @"기존 공인인증서 파일 가져오기(&C)";

        internal static readonly string MainForm_BrowseButtonText
            = @"찾아보기(&B)...";

        internal static readonly string MainForm_UseMicrophoneCheckboxText
            = @"오디오 입력 사용하기(&A) - 개인 정보 노출에 주의하세요!";

        internal static readonly string MainForm_UseWebCameraCheckboxText
            = @"비디오 입력 사용하기(&V) - 개인 정보 노출에 주의하세요!";

        internal static readonly string MainForm_UsePrinterCheckboxText
            = @"프린터 같이 사용하기(&P)";

        internal static readonly string MainForm_SelectSiteLabelText
            = $"{AppName} 위에서 접속할 사이트들을 선택해주세요. 사이트에서 필요한 프로그램들을 자동으로 설치해드려요.";

        internal static readonly string MainForm_SelectSiteLabelText_Alt
            = @"카탈로그 파일을 가져오지 못했어요! 그래도 샌드박스는 대신 실행해드려요.";

        internal static readonly string MainForm_JustRunItemText
            = @"그냥 실행해주세요.";

        internal static readonly string MainForm_AboutButtonText
            = @"정보";

        internal static readonly string MainForm_CloseButtonText
            = @"닫기";

        internal static readonly string MainForm_LaunchSandboxButtonText
            = @"샌드박스 실행";
    }

    // 정보 대화 상자에 표시될 문자열들
    partial class StringResources
    {
        internal static readonly string AboutDialog_BodyText
            = $"{AppName} (빌드 번호: {ThisAssembly.Git.Commit})\r\n\r\nhttps://bit.ly/yourtablecloth\r\n\r\n{AppCopyright}";
    }

    // 인증서 검색 창에 표시될 문자열들
    partial class StringResources
    {
        internal static readonly string CertSelectForm_Title
            = @"검색된 공인 인증서 선택";

        internal static readonly string CertSelectForm_InstructionLabel
            = @"검색된 공인 인증서가 다음과 같습니다. 다음 중 하나를 선택해주세요.";

        internal static readonly string CertSelectForm_RefreshButtonText
            = @"새로 고침(&R)";

        internal static readonly string CertSelectForm_ManualInstructionLabelText
            = @"원하는 인증서가 없다면, 직접 인증서 찾기 버튼을 눌러서 직접 DER 파일과 KEY 파일을 찾아주세요.";

        internal static readonly string CertSelectForm_OpenNpkiCertButton
            = @"직접 인증서 찾기(&B)...";

        internal static readonly string CertSelectForm_FileOpenDialog_Text
            = @"인증서 파일 (signCert.der, signPri.key) 열기";

        internal static readonly string CertSelectForm_FileOpenDialog_FilterText
            = @"인증서 파일 (*.der;*.key)|*.der;*.key|모든 파일|*.*";
    }

    // 오류 메시지에 표시될 문자열들
    partial class StringResources
    {
        internal static readonly string Error_Already_TableCloth_Running
            = "이미 식탁보 프로그램이 실행되고 있어요.";

        internal static readonly string Error_Windows_OS_Too_Old
            = "실행하고 있는 운영 체제는 윈도우 샌드박스 기능을 지원하지 않는 오래된 버전의 운영 체제 같습니다. 윈도우 10 이상으로 업그레이드 해주세요.";

        internal static readonly string Error_Windows_Sandbox_Missing
            = "윈도우 샌드박스가 설치되어있지 않은 것 같습니다! Windows 기능 켜기/끄기에서 Windows 샌드박스를 설정해주세요.";

        internal static readonly string Error_OpenDerAndKey_Simultaneously
            = "인증서 정보 파일 (der)과 개인 키 파일 (key)을 각각 하나씩 선택해주세요.\r\n\r\nCtrl 키나 Shift 키를 누른 채로 선택하거나, 파일 선택 창에서 빈 공간을 드래그하면 여러 파일을 선택할 수 있어요.";

        internal static readonly string Error_Windows_Sandbox_Already_Running
            = "식탁보를 통해서 윈도우 샌드박스를 실행하고 있는 것 같습니다. 사용을 마친 후 윈도우 샌드박스를 먼저 종료해주세요.";

        internal static string Error_HostFolder_Unavailable(IEnumerable<string> unavailableDirectories)
        {
            var directoryList = string.Join("\r\n", unavailableDirectories.Select(x => $"- {x}"));
            return $"다음의 디렉터리를 이 컴퓨터에서 찾을 수 없어 샌드박스에서 연결할 때 제외합니다.\r\n\r\n{directoryList}";
        }

        internal static readonly string Error_Windows_Explorer_Missing
            = "Windows 탐색기 프로그램을 찾을 수 없습니다.";

        internal static readonly string Error_Windows_Explorer_CanNotStart
            = "Windows 탐색기 프로그램을 시작할 수 없습니다.";

        internal static readonly string Error_Windows_Sandbox_CanNotStart
            = "샌드박스 프로그램을 실행하지 못했습니다.";

        internal static readonly string Error_Cannot_Find_CertFile
            = "인증서 파일 (.der) 파일을 찾을 수 없습니다.";

        internal static readonly string Error_Cannot_Find_KeyFile
            = "개인 키 파일 (.key) 파일을 찾을 수 없습니다.";

        internal static string Error_Cannot_Download_Catalog(Exception ex)
        {
            if (ex is AggregateException ae)
                return Error_Cannot_Download_Catalog(ae.InnerException);

            var message = $"카탈로그 파일을 내려받지 못했습니다.";

            if (ex != null)
                message = string.Concat(message, $"\r\n\r\n참고로, 발생했던 오류는 다음과 같습니다 - {ex.Message}");

            return message;
        }

        internal static string Error_Cannot_Create_AppDataDirectory(Exception ex)
        {
            if (ex is AggregateException ae)
                return Error_Cannot_Create_AppDataDirectory(ae.InnerException);

            var message = $"애플리케이션 데이터 저장을 위한 디렉터리를 만들지 못했습니다.";

            if (ex != null)
                message = string.Concat(message, $"\r\n\r\n참고로, 발생했던 오류는 다음과 같습니다 - {ex.Message}");

            return message;
        }
    }

    // 스크립트 내에서 사용되는 문자열들
    partial class StringResources
    {
        internal static readonly string Script_InstructionTitleText
            = "안내";

        internal static string Script_InstructionMessage(int packageTotalCount, string siteNameList)
            => $"지금부터 {packageTotalCount}개 프로그램의 설치 과정이 시작됩니다. 모든 프로그램의 설치가 끝나면 자동으로 {siteNameList} 홈페이지가 열립니다.";
    }

    // 호스트 프로그램의 일반 메시지 문자열들
    partial class StringResources
    {
        internal static readonly string Hostess_No_Targets
            = "이용하려는 웹 사이트 아이디가 지정되지 않았습니다. 샌드박스는 지금부터 사용하실 수 있어요.";

        internal static readonly string Hostess_Download_InProgress
            = "다운로드 중...";

        internal static readonly string Hostess_Install_InProgress
            = "설치하는 중...";

        internal static readonly string Hostess_Install_Succeed
            = "설치 완료";

        internal static readonly string Hostess_Install_Failed
            = "설치 실패";
    }

    // 호스트 프로그램의 오류 메시지 문자열들
    partial class StringResources
    {
        internal static readonly string HostessError_CatalogDeserilizationFailure
            = "Catalog.xml 파일의 형식이 프로그램이 이해하는 것과 다른 것 같습니다.";

        internal static string HostessError_CatalogLoadFailure(Exception ex)
        {
            if (ex is AggregateException ae)
                return HostessError_CatalogLoadFailure(ae.InnerException);

            var message = $"원격 웹 사이트로부터 Catalog.xml 파일을 불러올 수 없어 설치를 계속 진행할 수 없습니다.";

            if (ex != null)
                message = string.Concat(message, $"\r\n\r\n참고로, 발생했던 오류는 다음과 같습니다 - {ex.Message}");

            return message;
        }

        internal static string HostessError_PackageInstallFailure(string errorMessage)
            => $"패키지를 설치하는 도중 오류가 발생했습니다. {(string.IsNullOrWhiteSpace(errorMessage) ? "그러나 원인을 파악하지 못했습니다." : errorMessage)}";

        internal static string HostessError_Package_CanNotStart
            = "패키지 설치 프로그램을 시작하지 못했습니다.";
    }

#pragma warning restore IDE0040, IDE0066
}
