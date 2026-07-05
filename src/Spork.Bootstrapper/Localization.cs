namespace Spork.Bootstrapper;

/// <summary>
/// UI 문자열 리소스 테이블(다국어). 브랜드는 "식탁보"로 통일한다. InvariantGlobalization(크기 절감)
/// 을 유지하기 위해 .NET 컬처/ResourceManager(위성 어셈블리 = NativeAOT 동적 로딩 불가) 대신,
/// 언어별 데이터 테이블을 코드로 두고 Win32 <c>GetUserDefaultUILanguage</c> 로 선택한다.
/// 새 언어는 <see cref="UiStrings"/> 인스턴스 하나를 추가하고 <see cref="Loc.Resolve"/> 에 매핑하면 된다.
/// </summary>
internal sealed class UiStrings
{
    public required string FontFamily { get; init; }
    public required string WindowTitle { get; init; }
    public required string Preparing { get; init; }
    public required string CheckingLatest { get; init; }
    public required string Downloading { get; init; }
    public required string Verifying { get; init; }
    public required string Extracting { get; init; }
    public required string Launching { get; init; }
    public required string Retrying { get; init; }
    public required string DownloadProgressFormat { get; init; }      // {0}=받음MB {1}=전체MB {2}=%
    public required string DownloadIndeterminateFormat { get; init; } // {0}=받음MB
    public required string ErrorTitle { get; init; }
    public required string LaunchCanceledTitle { get; init; }
    public required string LaunchCanceledDetail { get; init; }
    public required string RetryButton { get; init; }
    public required string ReRunButton { get; init; }
    public required string CloseButton { get; init; }
    public required string ErrAssetNotFoundFormat { get; init; }      // {0}=패턴
    public required string ErrSporkNotFound { get; init; }
    public required string ErrUnsupportedArchFormat { get; init; }    // {0}=arch
    public required string ErrSha256 { get; init; }
    public required string ErrUnsafeDest { get; init; }               // {0}=dest 경로
    public required string ConfirmCancelTitle { get; init; }
    public required string ConfirmCancelMessage { get; init; }
}

internal static class Loc
{
    private static UiStrings? _current;

    /// <summary>선택된 언어 테이블. <see cref="Initialize"/> 전에도 자동 감지로 폴백한다.</summary>
    public static UiStrings S => _current ??= Resolve(null);

    /// <summary>언어 확정(옵션 <c>--lang</c> 우선, 없으면 OS UI 언어 자동 감지). Main 초반에 1회 호출.</summary>
    public static void Initialize(string? langOverride) => _current = Resolve(langOverride);

    private static UiStrings Resolve(string? langOverride)
    {
        string code = (langOverride ?? "").Trim().ToLowerInvariant();
        if (code.Length == 0)
            code = DetectOsUiLanguage();

        if (code.StartsWith("ko"))
            return Korean;
        return English; // 기본/폴백은 영어(국제).
    }

    // .NET 글로벌라이제이션 없이 OS 표시 언어를 판별(InvariantGlobalization 유지).
    // LANGID 하위 10비트가 primary language id. LANG_KOREAN = 0x12.
    private static string DetectOsUiLanguage()
    {
        try
        {
            int primary = Interop.GetUserDefaultUILanguage() & 0x3FF;
            return primary == 0x12 ? "ko" : "en";
        }
        catch
        {
            return "en";
        }
    }

    private static readonly UiStrings Korean = new()
    {
        FontFamily = "Malgun Gothic",
        WindowTitle = "식탁보 준비",
        Preparing = "준비 중...",
        CheckingLatest = "최신 버전 확인 중...",
        Downloading = "식탁보 다운로드 중...",
        Verifying = "무결성 검증 중...",
        Extracting = "압축 해제 중...",
        Launching = "식탁보 실행 중...",
        Retrying = "다시 시도 중...",
        DownloadProgressFormat = "{0} / {1} MB ({2}%)",
        DownloadIndeterminateFormat = "{0} MB 받는 중...",
        ErrorTitle = "오류가 발생했습니다. 다시 시도하거나 창을 닫으세요.",
        LaunchCanceledTitle = "식탁보 실행을 취소했습니다.",
        LaunchCanceledDetail = "다운로드는 완료되었습니다. 아래 [다시 실행]을 눌러 식탁보를 실행하세요.",
        RetryButton = "다시 시도",
        ReRunButton = "다시 실행",
        CloseButton = "닫기",
        ErrAssetNotFoundFormat = "최신 릴리스에서 '{0}' 자산을 찾지 못했습니다.",
        ErrSporkNotFound = "식탁보 실행 파일을 찾지 못했습니다.",
        ErrUnsupportedArchFormat = "지원하지 않는 아키텍처입니다: {0} (x64/arm64만 지원).",
        ErrSha256 = "무결성 검증에 실패했습니다(SHA256 불일치).",
        ErrUnsafeDest = "대상 폴더가 비어있지 않고 식탁보 폴더가 아니라 삭제를 거부했습니다: {0}",
        ConfirmCancelTitle = "다운로드 취소",
        ConfirmCancelMessage = "식탁보를 다운로드하는 중입니다. 취소하고 창을 닫을까요?",
    };

    private static readonly UiStrings English = new()
    {
        FontFamily = "Segoe UI",
        WindowTitle = "TableCloth Setup",
        Preparing = "Preparing...",
        CheckingLatest = "Checking latest version...",
        Downloading = "Downloading TableCloth...",
        Verifying = "Verifying integrity...",
        Extracting = "Extracting...",
        Launching = "Starting TableCloth...",
        Retrying = "Retrying...",
        DownloadProgressFormat = "{0} / {1} MB ({2}%)",
        DownloadIndeterminateFormat = "{0} MB downloaded...",
        ErrorTitle = "Something went wrong. Retry or close this window.",
        LaunchCanceledTitle = "TableCloth launch was canceled.",
        LaunchCanceledDetail = "The download is complete. Click [Run again] to start TableCloth.",
        RetryButton = "Retry",
        ReRunButton = "Run again",
        CloseButton = "Close",
        ErrAssetNotFoundFormat = "Could not find asset '{0}' in the latest release.",
        ErrSporkNotFound = "Could not find the TableCloth executable.",
        ErrUnsupportedArchFormat = "Unsupported architecture: {0} (only x64/arm64).",
        ErrSha256 = "Integrity check failed (SHA256 mismatch).",
        ErrUnsafeDest = "Refused to delete a non-empty, non-TableCloth destination folder: {0}",
        ConfirmCancelTitle = "Cancel download",
        ConfirmCancelMessage = "TableCloth is still downloading. Cancel and close?",
    };
}
