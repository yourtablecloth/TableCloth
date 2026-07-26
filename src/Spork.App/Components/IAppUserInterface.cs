using Avalonia.Controls;
using Spork.Dialogs;
using Spork.ViewModels;
using System.Collections.Generic;

namespace Spork.Components
{
    public interface IAppUserInterface
    {
        AboutWindow CreateAboutWindow();
        MainWindow CreateMainWindow();
        SplashScreen CreateSplashScreen();
        PrecautionsWindow CreatePrecautionsWindow(IEnumerable<string> targetServiceIds = null);
        SiteReportWindow CreateSiteReportWindow();
        InstallStepsWindow CreateInstallStepsWindow(IList<StepItemViewModel> steps, bool dryRun, string targetTitle = null, string targetIconKey = null);

        /// <summary>
        /// 이슈 #296: WPF <c>window.ShowDialog()</c>(동기) 대체. 소유자를 내부에서 해석(활성/메인 창)하고
        /// 모달로 <b>동기</b> 표시한 뒤 다이얼로그 결과(<see cref="System.Windows"/> 시절 DialogResult 상당)를 반환한다.
        /// </summary>
        bool? ShowDialog(Window window);

        /// <summary>
        /// AhnLab Safe Transaction "원격 접속 차단" 해제 안내 창을 UI 스레드에서 모달로 띄운다(이슈 #275).
        /// 스텝(백그라운드 스레드)에서 호출할 수 있도록 내부에서 디스패치한다. 사용자가 해제 확인 후 '계속'을
        /// 누르면 <see langword="true"/>, 건너뛰거나 창을 닫으면 <see langword="false"/>를 반환한다.
        /// </summary>
        /// <param name="stSessPath">ASTx 설정 실행 파일(StSess.exe)의 전체 경로.</param>
        bool ShowAhnLabSafeTxGuideDialog(string stSessPath);
    }
}
