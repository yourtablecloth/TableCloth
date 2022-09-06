using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TableCloth.Models.Catalog;

namespace TableCloth.Resources
{
    // 공통 문자열들
    internal static partial class StringResources
    {
        internal static readonly string AppName = "식탁보";

        internal static readonly string AppCopyright = "(c) 2021 남정현";

        internal static readonly string AppPublisher = "식탁보 프로젝트";

        internal static readonly string AppCommentText = "식탁보 - 컴퓨터를 깨끗하게 사용하세요!";

        internal static readonly string AppInfoUrl =
            "https://yourtablecloth.github.io";

        internal static readonly string PrivacyPolicyUrl =
            "https://yourtablecloth.app/privacy";

        internal static readonly string IEModePolicyXmlUrl =
            "https://yourtablecloth.app/TableClothCatalog/sites.xml";

        internal static readonly string AppUpdateInfoUrl =
            "https://github.com/yourtablecloth/TableCloth/releases";

        internal static readonly string CatalogUrl =
            "https://yourtablecloth.github.io/TableClothCatalog/Catalog.xml";

        internal static readonly string IEModeListUrl =
            "https://yourtablecloth.github.io/TableClothCatalog/Compatibility.xml";

        internal static readonly string ImageUrlPrefix =
            "https://yourtablecloth.github.io/TableClothCatalog/images";

        internal static readonly string SentryDsn =
            "https://785e3f46849c403bb6c323d7a9eaad91@o77541.ingest.sentry.io/5915832";

        internal static readonly string UserAgentText =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.164 Safari/537.36";

        internal static readonly string EveryonesPrinterUrl =
            "https://modu-print.tistory.com/category/%EB%8B%A4%EC%9A%B4%EB%A1%9C%EB%93%9C/%EB%AA%A8%EB%91%90%EC%9D%98%20%ED%94%84%EB%A6%B0%ED%84%B0";

        internal static readonly string AdobeReaderUrl =
            "https://get.adobe.com/kr/reader/";

        internal static readonly string HancomOfficeViewerUrl =
            "https://www.hancom.com/cs_center/csDownload.do";
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

        internal static string Get_AppVersion()
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var versionInfo = executingAssembly.GetName().Version.ToString();

            try
            {
                var resourceNames = executingAssembly.GetManifestResourceNames();
                var commitTextFileName = resourceNames.Where(x => x.EndsWith("commit.txt", StringComparison.Ordinal)).FirstOrDefault();

                if (commitTextFileName != null)
                {
                    using (var resourceStream = executingAssembly.GetManifestResourceStream(commitTextFileName))
                    {
                        var streamReader = new StreamReader(resourceStream, new UTF8Encoding(false), true);
                        var commitId = streamReader.ReadToEnd().Trim();

                        if (commitId.Length > 8)
                            commitId = commitId.Substring(0, 8);

                        versionInfo = $"{versionInfo}, #{commitId.Substring(0, 8)}";
                    }
                }
            }
            catch { }

            return versionInfo;
        }
    }

    // 비 사용자 인터페이스 문자열들
    partial class StringResources
    {
        internal static readonly string UnknownText = "알 수 없음";

        internal static string LinkNamePostfix_ManyOthers(int count)
            => $" 외 {count}개";

        internal static string InternetService_DisplayText(CatalogInternetService svc)
        {
            var defaultString = $"{svc.DisplayName} - {svc.Url}";
            var pkgs = svc.Packages;

            var hasCompatNotes = !string.IsNullOrWhiteSpace(svc.CompatibilityNotes);

            if (hasCompatNotes)
                defaultString = $"*{defaultString}";

            if (pkgs != null && pkgs.Count > 0)
                defaultString = $"{defaultString} (총 {svc.PackageCountForDisplay}개 프로그램 설치)";

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

    // 오류 메시지에 표시될 문자열들
    partial class StringResources
    {
        internal static readonly string Info_UpdateRequired
            = "새 버전의 식탁보가 출시되었습니다.";

        internal static readonly string Info_UpdateNotRequired
            = "최신 버전의 식탁보를 사용 중입니다.";

        internal static readonly string Ask_RestartRequired =
            "설정이 반영되려면 식탁보 프로그램을 다시 시작해야 합니다." + Environment.NewLine +
            "지금 다시 시작하시겠습니까?";

        internal static readonly string Error_Already_TableCloth_Running
            = "이미 식탁보 프로그램이 실행되고 있어요.";

        internal static readonly string Error_Windows_OS_Too_Old
            = "실행하고 있는 운영 체제는 윈도우 샌드박스 기능을 지원하지 않는 오래된 버전의 운영 체제 같습니다. 윈도우 10 이상으로 업그레이드 해주세요.";

        internal static readonly string Error_Windows_Sandbox_Missing
            = "윈도우 샌드박스가 설치되어있지 않은 것 같습니다! 윈도우 기능 켜기/끄기에서 윈도우 샌드박스를 활성화해주세요.";

        internal static readonly string Error_OpenDerAndKey_Simultaneously =
            "공동 인증서 정보 파일 (der)과 공동 인증서 개인 키 파일 (key)을 각각 하나씩 선택해주세요." + Environment.NewLine +
            Environment.NewLine +
            "Ctrl 키나 Shift 키를 누른 채로 선택하거나, 파일 선택 창에서 빈 공간을 드래그하면 여러 파일을 선택할 수 있어요.";

        internal static readonly string Error_Windows_Sandbox_Already_Running
            = "식탁보를 통해서 윈도우 샌드박스를 실행하고 있는 것 같습니다. 사용을 마친 후 윈도우 샌드박스를 먼저 종료해주세요.";

        internal static readonly string Error_IEMode_NotAvailable
            = "Microsoft Edge 브라우저 안에서 인터넷 익스플로러 모드를 활성화해야 호환성 문제를 피할 수 있습니다. 인터넷 익스플로러를 시스템 구성 요소 추가/제거를 통해 활성화해주세요.";

        internal static string Error_Cannot_Invoke_GetVersionEx(int errorCode)
            => $"윈도우 OS 버전 정보 조회 API를 호출했지만, 다음의 오류 코드와 함께 실패했습니다 - {errorCode:X8}";

        internal static readonly string Error_Cannot_Invoke_GetProductInfo
            = "윈도우 OS 제품 정보 조회 API를 호출했지만, 정보를 가져올 수 없습니다.";

        internal static readonly string Error_SandboxMightNotAvailable
            = "검색된 제품 정보에 따르면, 윈도우 샌드박스 기능이 지원되지 않는 버전의 운영 체제를 사용 중인 것 같습니다.";

        internal static string Error_HostFolder_Unavailable(IEnumerable<string> unavailableDirectories)
        {
            var directoryList = string.Join(Environment.NewLine, unavailableDirectories.Select(x => $"- {x}"));
            return "다음의 디렉터리를 이 컴퓨터에서 찾을 수 없어 샌드박스에서 연결할 때 제외합니다." + Environment.NewLine +
                Environment.NewLine +
                $"{directoryList}";
        }

        internal static readonly string Error_Windows_Explorer_Missing
            = "윈도우 탐색기 프로그램을 찾을 수 없습니다.";

        internal static readonly string Error_Windows_Explorer_CanNotStart
            = "윈도우 탐색기 프로그램을 시작할 수 없습니다.";

        internal static readonly string Error_Windows_Sandbox_CanNotStart
            = "샌드박스 프로그램을 실행하지 못했습니다.";

        internal static readonly string Error_Cannot_Find_CertFile
            = "공동 인증서 정보 파일 (.der) 파일을 찾을 수 없습니다.";

        internal static readonly string Error_Cannot_Find_KeyFile
            = "공동 인증서 개인 키 파일 (.key) 파일을 찾을 수 없습니다.";

        internal static readonly string Error_Cannot_Find_PfxFile
            = "공동 인증서 파일 (.pfx) 파일을 찾을 수 없습니다.";

        internal static string Error_Cannot_Download_Catalog(Exception ex)
        {
            if (ex is AggregateException ae)
                return Error_Cannot_Download_Catalog(ae.InnerException);

            var message = $"카탈로그 파일을 내려받지 못했습니다.";

            if (ex != null)
            {
                message = string.Concat(message, Environment.NewLine +
                    Environment.NewLine +
                    $"참고로, 발생했던 오류는 다음과 같습니다 - {ex.Message}");
            }

            return message;
        }

        internal static string Error_Cannot_Create_AppDataDirectory(Exception ex)
        {
            if (ex is AggregateException ae)
                return Error_Cannot_Create_AppDataDirectory(ae.InnerException);

            var message = $"애플리케이션 데이터 저장을 위한 디렉터리를 만들지 못했습니다.";

            if (ex != null)
            {
                message = string.Concat(message, Environment.NewLine
                    + Environment.NewLine
                    + $"참고로, 발생했던 오류는 다음과 같습니다 - {ex.Message}");
            }

            return message;
        }

        internal static readonly string Error_Cannot_Run_SysInfo =
            "시스템 정보 유틸리티를 실행할 수 없습니다.";

        internal static readonly string Error_ShortcutFailed =
            "바로 가기 생성에 실패했습니다.";
        
        internal static readonly string Info_ShortcutSuccess =
            "바탕 화면에 바로 가기를 생성했습니다.";

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

        internal static readonly string Hostess_No_PowerShell_Error
            = "Windows PowerShell 실행 파일을 찾을 수 없어 설치 스크립트를 실행할 수 없습니다.";

        internal static readonly string Hostess_CustomScript_Title
            = "별도 설치 스크립트";

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
            {
                message = string.Concat(message, Environment.NewLine +
                    Environment.NewLine +
                    $"참고로, 발생했던 오류는 다음과 같습니다 - {ex.Message}");
            }

            return message;
        }

        internal static string HostessError_PackageInstallFailure(string errorMessage)
            => $"패키지를 설치하는 도중 오류가 발생했습니다. {(string.IsNullOrWhiteSpace(errorMessage) ? "그러나 원인을 파악하지 못했습니다." : errorMessage)}";

        internal static string HostessError_Package_CanNotStart
            = "패키지 설치 프로그램을 시작하지 못했습니다.";
    }

    // 호스트 프로그램에서 사용할 스위치
    partial class StringResources
    {
        internal static readonly string TableCloth_Switch_Prefix = "--";

        internal static readonly string TableCloth_Switch_IgnoreSwitch = "--ignore--";

        internal static readonly string TableCloth_Switch_EnableMicrophone = TableCloth_Switch_Prefix + "enable-microphone";

        internal static readonly string TableCloth_Switch_EnableCamera = TableCloth_Switch_Prefix + "enable-camera";

        internal static readonly string TableCloth_Switch_EnablePrinter = TableCloth_Switch_Prefix + "enable-printer";

        internal static readonly string Tablecloth_Switch_EnableCert = TableCloth_Switch_Prefix + "enable-cert";

        internal static readonly string TableCloth_Switch_CertPublicKey = TableCloth_Switch_Prefix + "cert-public-key";

        internal static readonly string TableCloth_Switch_CertPrivateKey = TableCloth_Switch_Prefix + "cert-private-key";

        internal static readonly string TableCloth_Switch_EnableEveryonesPrinter = TableCloth_Switch_Prefix + "enable-everyones-printer";

        internal static readonly string TableCloth_Switch_EnableAdobeReader = TableCloth_Switch_Prefix + "enable-adobe-reader";

        internal static readonly string TableCloth_Switch_EnableHancomOfficeViewer = TableCloth_Switch_Prefix + "enable-hancom-office-viewer";

        internal static readonly string TableCloth_Switch_EnableIEMode = TableCloth_Switch_Prefix + "enable-ie-mode";

        internal static readonly string TableCloth_Switch_Help = TableCloth_Switch_Prefix + "help";

        internal static readonly string TableCloth_TableCloth_Switches_Help = $@"ServiceID ServiceID ... <옵션>

ServiceID는 {CatalogUrl}을 확인해주세요.

옵션:

{TableCloth_Switch_EnableMicrophone}
  오디오 입력 사용하기 기능을 켭니다.

{TableCloth_Switch_EnableCamera}
  비디오 입력 사용하기 기능을 켭니다.

{TableCloth_Switch_EnablePrinter}
  프린터 공유하기 기능을 켭니다.

{Tablecloth_Switch_EnableCert}
  인증서를 기능을 켭니다.

{TableCloth_Switch_CertPublicKey} <파일 경로>
  인증서 공개 키 파일 경로를 지정합니다.

{TableCloth_Switch_CertPrivateKey} <파일 경로>
  인증서 비밀 키 파일 경로를 지정합니다.

{TableCloth_Switch_EnableEveryonesPrinter}
  모두의 프린터 설치를 샌드박스 시작 후 실행합니다.

{TableCloth_Switch_EnableAdobeReader}
  Adobe Reader 설치를 샌드박스 시작 후 실행합니다.

{TableCloth_Switch_EnableHancomOfficeViewer}
  한컴오피스 뷰어 설치를 샌드박스 시작 후 실행합니다.

{TableCloth_Switch_EnableIEMode}
  Internet Explorer 모드를 활성화합니다.

{TableCloth_Switch_Help}
  이 도움말을 표시합니다.
";

        internal static readonly string TableCloth_Hostess_Switches_Help = $@"ServiceID ServiceID ... <옵션>

ServiceID는 {CatalogUrl}을 확인해주세요.

옵션:

{TableCloth_Switch_EnableEveryonesPrinter}
  모두의 프린터 설치를 샌드박스 시작 후 실행합니다.

{TableCloth_Switch_EnableAdobeReader}
  Adobe Reader 설치를 샌드박스 시작 후 실행합니다.

{TableCloth_Switch_EnableHancomOfficeViewer}
  한컴오피스 뷰어 설치를 샌드박스 시작 후 실행합니다.

{TableCloth_Switch_EnableIEMode}
  Internet Explorer 모드를 활성화합니다.

{TableCloth_Switch_Help}
  이 도움말을 표시합니다.
";
    }

#pragma warning restore IDE0040, IDE0066
}
