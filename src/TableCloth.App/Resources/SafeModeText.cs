using System.Globalization;

namespace TableCloth.Resources;

public static class SafeModeText
{
    private static string L(string ko, string en)
        => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko" ? ko : en;

    public static string Option => L("Safe Mode 사용", "Use Safe Mode");
    public static string Description => L(
        "공유 폴더 사용 시 PC가 중지되는 경우 선택할 수 있습니다. 호스트 폴더, 공동인증서, 클립보드, 프린터, 카메라, 마이크와 GPU를 공유하지 않습니다. 네트워크는 유지합니다. 일반 모드의 설정은 보존합니다.",
        "Use this mode if sharing folders causes your PC to crash. Host folders, certificates, clipboard, printers, camera, microphone and GPU are not shared. Networking stays enabled. Normal-mode settings are preserved.");
    public static string Status => L("실행 모드: Safe Mode", "Run mode: Safe Mode");
    public static string NormalStatus => L("실행 모드: 일반", "Run mode: Normal");
    public static string SessionNotice => L("Safe Mode에서는 호스트의 유휴 자동 종료 설정을 전달하지 않습니다.",
        "Safe Mode does not transfer the host idle shutdown setting.");
    public static string BrowserConfirmation => L(
        "Safe Mode의 브라우저 전용 Sandbox로 시작합니다. 호스트 폴더와 공동인증서, 클립보드, 프린터, 카메라, 마이크와 GPU를 공유하지 않습니다. 네트워크를 유지하며 게스트의 Edge로 주소를 엽니다. Sandbox의 데이터는 종료 시 삭제됩니다. Windows Sandbox 자체와 보안 드라이버의 호환성 문제까지 해결하지는 않습니다. 계속하시겠습니까?",
        "Start a browser-only sandbox in Safe Mode? Host folders, certificates, clipboard, printers, camera, microphone and GPU are not shared. Networking stays enabled and the guest Edge opens the address. Sandbox data is deleted when it closes. Windows Sandbox and security-driver compatibility issues may still occur.");
    public static string GuestVersionNotice => L(
        "Sandbox 안에서 최신 안정 버전의 Spork를 다운로드합니다. 호스트의 프리뷰 기능과 버전이 다를 수 있습니다.",
        "The sandbox downloads the latest stable Spork. Its version and features may differ from this host preview.");
    public static string Confirmation => L(
        "Safe Mode로 시작합니다.\n\n호스트의 App, Data, NPKI와 사용자 폴더를 공유하지 않습니다. 호스트 공동인증서와 ZScaler 루트 인증서를 전달하지 않습니다. Sandbox에서 만든 파일과 즐겨찾기는 종료 시 삭제됩니다. 클립보드, 프린터, 카메라, 마이크와 GPU 공유를 끕니다. 네트워크는 유지합니다.\n\n" + GuestVersionNotice + " 유휴 자동 종료 등 호스트의 실행 옵션은 전달하지 않습니다.\n\nWindows Sandbox 자체와 보안 드라이버의 호환성 문제까지 해결하지는 않습니다. 계속하시겠습니까?",
        "Start in Safe Mode?\n\nApp, Data, NPKI and custom host folders are not shared. Host certificates and ZScaler roots are not transferred. Files and favorites created in the sandbox are deleted when it closes. Clipboard, printer, camera, microphone and GPU sharing are disabled. Networking stays enabled.\n\n" + GuestVersionNotice + " Host session options such as idle shutdown are not transferred.\n\nWindows Sandbox and security-driver compatibility issues may still occur.");
}
