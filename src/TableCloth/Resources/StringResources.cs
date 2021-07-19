using System;
using System.Linq;
using TableCloth.Models.TableClothCatalog;

namespace TableCloth.Resources
{
    // 공통 문자열들
    static partial class StringResources
    {
        internal static readonly string AppName = "식탁보";

        internal static readonly string AppCopyright = "(c) 2021 남정현";

        internal static readonly string OkayButtonText = "확인";

        internal static readonly string CancelButtonText = "취소";

        internal static readonly string CatalogUrl =
            "https://dotnetdev-kr.github.io/TableCloth/Catalog.txt";

        internal static readonly string UserAgentText =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.164 Safari/537.36";
    }

    // 공통 대화 상자 제목들
    partial class StringResources
    {
        internal static readonly string TitleText_Info
            = $"{AppName} 정보";

        internal static readonly string TitleText_Error
            = $"{AppName} 오류";
    }

    // 비 사용자 인터페이스 문자열들
    partial class StringResources
    {
        internal static readonly string InternetService_UnknownText = "알 수 없음";

        internal static string InternetService_DisplayText(CatalogInternetService svc)
        {
            var defaultString = $"{svc.DisplayName} - {svc.Url}";
            var pkgs = svc.Packages;

            if (pkgs != null && pkgs.Count() > 0)
                defaultString = $"{defaultString} (총 {pkgs.Count()}개 프로그램 설치)";

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
                    return "금융";

                case CatalogInternetServiceCategory.Government:
                    return "공공";

                default:
                    return "기타";
            }
        }
    }

    // 메인 화면에 표시될 문자열들
    partial class StringResources
    {
        internal static readonly string MainForm_Title
            = @$"{AppName} - 컴퓨터를 깨끗하게 사용하세요!";

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
            = @$"{AppName} 위에서 접속할 사이트들을 선택해주세요. 사이트에서 필요한 프로그램들을 자동으로 설치해드려요.";

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
        internal static readonly string Error_Windows_OS_Too_Old
            = "실행하고 있는 운영 체제는 윈도우 샌드박스 기능을 지원하지 않는 오래된 버전의 운영 체제 같습니다. 윈도우 10 이상으로 업그레이드 해주세요.";

        internal static readonly string Error_Windows_Sandbox_Missing
            = "윈도우 샌드박스가 설치되어있지 않은 것 같습니다! 프로그램 추가/제거 - Windows 기능 켜기/끄기에서 설정해주세요.";

        internal static readonly string Error_OpenDerAndKey_Simultaneously
            = "인증서 정보 파일 (der)과 개인 키 파일 (key)을 각각 하나씩 선택해주세요.\r\n\r\nCtrl 키나 Shift 키를 누른 채로 선택하거나, 파일 선택 창에서 빈 공간을 드래그하면 여러 파일을 선택할 수 있어요.";

        internal static string Error_Cannot_Remove_TempDirectory(Exception ex)
        {
            if (ex is AggregateException ae)
                return Error_Cannot_Remove_TempDirectory(ae.InnerException);

            var message = $"임시 폴더를 비우지 못했습니다. 해당 폴더를 열어 직접 지우실 수 있게 도와드릴까요?";

            if (ex != null)
                message = string.Concat(message, $"\r\n\r\n참고로, 발생했던 오류는 다음과 같습니다 - {ex.Message}");

            return message;
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
    }

    // 스크립트 내에서 사용되는 문자열들
    partial class StringResources
    {
        internal static readonly string Script_InstructionTitleText
            = "안내";

        internal static string Script_InstructionMessage(int packageTotalCount, string siteNameList)
            => $"지금부터 {packageTotalCount}개 프로그램의 설치 과정이 시작됩니다. 모든 프로그램의 설치가 끝나면 자동으로 {siteNameList} 홈페이지가 열립니다.";
    }
}
