using Spork.Dialogs;
using Spork.ViewModels;
using System.Collections.Generic;

namespace Spork.Components
{
    public interface IAppUserInterface
    {
        AboutWindow CreateAboutWindow();
        MainWindow CreateMainWindow();
        SandboxGuidanceWindow CreateSandboxGuidanceWindow();
        PrecautionsWindow CreatePrecautionsWindow(IEnumerable<string> targetServiceIds = null);
        SiteReportWindow CreateSiteReportWindow();
        InstallStepsWindow CreateInstallStepsWindow(IList<StepItemViewModel> steps, bool dryRun, string targetTitle = null, string targetIconKey = null);

        /// <summary>
        /// AhnLab Safe Transaction "원격 접속 차단" 해제 안내 창을 UI 스레드에서 모달로 띄운다(이슈 #275).
        /// 스텝(백그라운드 스레드)에서 호출할 수 있도록 내부에서 디스패치한다. 사용자가 해제 확인 후 '계속'을
        /// 누르면 <see langword="true"/>, 건너뛰거나 창을 닫으면 <see langword="false"/>를 반환한다.
        /// </summary>
        /// <param name="stSessPath">ASTx 설정 실행 파일(StSess.exe)의 전체 경로.</param>
        bool ShowAhnLabSafeTxGuideDialog(string stSessPath);
    }
}