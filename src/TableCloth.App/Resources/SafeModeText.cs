using System.Globalization;

namespace TableCloth.Resources;

public static class SafeModeText
{
    private static string L(string ko, string en)
        => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko" ? ko : en;

    public static string StartLink => L("샌드박스를 시작하는데 문제가 있으신가요?", "Having trouble starting the sandbox?");
    public static string Status => L("실행 모드: Safe Mode", "Run mode: Safe Mode");
    public static string BrowserConfirmation => L(
        "Safe Mode의 브라우저 전용 Sandbox로 시작합니다. 호스트 폴더와 공동인증서, 클립보드, 프린터, 카메라, 마이크와 GPU를 공유하지 않습니다. 네트워크를 유지하며 게스트의 Edge로 주소를 엽니다. Sandbox의 데이터는 종료 시 삭제됩니다. Windows Sandbox 자체와 보안 드라이버의 호환성 문제까지 해결하지는 않습니다. 계속하시겠습니까?",
        "Start a browser-only sandbox in Safe Mode? Host folders, certificates, clipboard, printers, camera, microphone and GPU are not shared. Networking stays enabled and the guest Edge opens the address. Sandbox data is deleted when it closes. Windows Sandbox and security-driver compatibility issues may still occur.");
    public static string GuestVersionNotice => L(
        "Sandbox 안에서 최신 안정 버전의 Spork를 다운로드합니다. 호스트의 프리뷰 기능과 버전이 다를 수 있습니다.",
        "The sandbox downloads the latest stable Spork. Its version and features may differ from this host preview.");
    public static string Confirmation => L(
        """
        Safe Mode는 호스트의 공유 기능을 끄고 샌드박스를 시작합니다.
        입력 내용과 저장된 실행 옵션은 적용하지 않습니다.

        다음 기능을 비활성화합니다.

        - 호스트 폴더와 인증서 공유
        - 클립보드 공유
        - 프린터, 카메라와 마이크 공유
        - GPU 가속

        네트워크는 유지하며 기관은 샌드박스 안에서 선택합니다.
        종료하면 파일과 즐겨찾기를 삭제합니다.

        Safe Mode로 계속 실행하시겠습니까?
        """,
        """
        Safe Mode starts the sandbox with host sharing turned off.
        Your input and saved launch options are not applied.

        The following features are disabled:

        - Host folder and certificate sharing
        - Clipboard sharing
        - Printer, camera and microphone sharing
        - GPU acceleration

        Networking stays enabled. Select a site inside the sandbox.
        Files and favorites are deleted when the sandbox closes.

        Continue in Safe Mode?
        """);
}
