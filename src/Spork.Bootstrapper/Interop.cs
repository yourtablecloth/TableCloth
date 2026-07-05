using System.Runtime.InteropServices;

namespace Spork.Bootstrapper;

/// <summary>
/// 부트스트래퍼 UI 에 필요한 최소 Win32/GDI/공통컨트롤 P/Invoke. CsWin32 등 외부 생성기 없이
/// 손수 작성한 <see cref="LibraryImportAttribute"/> 로 두어 외부 패키지 의존이 0 이고, 내장
/// <c>LibraryImportGenerator</c> 가 마샬링 코드를 소스 생성하므로 NativeAOT 에서 리플렉션이 없다.
/// 대상은 x64/arm64(64-bit) 전용이라 SetWindowLongPtrW 등 Ptr 계열을 그대로 쓸 수 있다.
/// </summary>
internal static unsafe partial class Interop
{
    // --- 윈도우 메시지 ---
    internal const uint WM_CREATE = 0x0001;
    internal const uint WM_DESTROY = 0x0002;
    internal const uint WM_CLOSE = 0x0010;
    internal const uint WM_SETFONT = 0x0030;
    internal const uint WM_SETICON = 0x0080;
    internal const uint WM_COMMAND = 0x0111;
    internal const uint WM_DPICHANGED = 0x02E0;
    internal const uint WM_APP = 0x8000;

    // WM_SETICON wParam
    internal const nint ICON_SMALL = 0;
    internal const nint ICON_BIG = 1;

    // COM (ITaskbarList3 생성용)
    internal const uint CLSCTX_INPROC_SERVER = 0x1;
    internal const uint COINIT_APARTMENTTHREADED = 0x2;

    // --- 윈도우 스타일 ---
    internal const uint WS_CHILD = 0x40000000;
    internal const uint WS_VISIBLE = 0x10000000;
    internal const uint WS_TABSTOP = 0x00010000;
    internal const uint WS_CAPTION = 0x00C00000;
    internal const uint WS_SYSMENU = 0x00080000;
    internal const uint WS_MINIMIZEBOX = 0x00020000;

    // --- 컨트롤 스타일 ---
    internal const uint SS_LEFT = 0x00000000;
    internal const uint BS_PUSHBUTTON = 0x00000000;
    internal const uint BS_DEFPUSHBUTTON = 0x00000001;

    // --- 클래스 스타일 / 시스템 값 ---
    internal const uint CS_VREDRAW = 0x0001;
    internal const uint CS_HREDRAW = 0x0002;
    internal const int IDC_ARROW = 32512;
    internal const int COLOR_BTNFACE = 15;
    internal const int GWL_STYLE = -16;
    internal const int SW_HIDE = 0;
    internal const int SW_SHOWNORMAL = 1;
    internal const int SW_SHOW = 5;
    internal const int SM_CXSCREEN = 0;
    internal const int SM_CYSCREEN = 1;
    internal const int CW_USEDEFAULT = unchecked((int)0x80000000);

    // --- SetWindowPos 플래그 ---
    internal const uint SWP_NOZORDER = 0x0004;
    internal const uint SWP_NOACTIVATE = 0x0010;

    // --- MessageBox (취소 확인 대화상자) ---
    internal const uint MB_YESNO = 0x00000004;
    internal const uint MB_ICONWARNING = 0x00000030;
    internal const uint MB_DEFBUTTON2 = 0x00000100; // 기본 포커스를 '아니오'에 둔다(실수 취소 방지)
    internal const int IDYES = 6;

    // --- 폰트(CreateFontW) ---
    internal const int FW_NORMAL = 400;
    internal const uint DEFAULT_CHARSET = 1;
    internal const uint CLEARTYPE_QUALITY = 5;

    // --- 진행 막대(msctls_progress32) ---
    internal const string ProgressClass = "msctls_progress32";
    internal const uint PBS_SMOOTH = 0x01;
    internal const uint PBS_MARQUEE = 0x08;
    internal const uint PBM_SETPOS = 0x0402;      // WM_USER+2
    internal const uint PBM_SETRANGE32 = 0x0406;  // WM_USER+6
    internal const uint PBM_SETMARQUEE = 0x040A;  // WM_USER+10

    // --- 공통 컨트롤 초기화 ---
    internal const uint ICC_PROGRESS_CLASS = 0x00000020;
    internal const uint ICC_STANDARD_CLASSES = 0x00004000;

    [StructLayout(LayoutKind.Sequential)]
    internal struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public nint lpszMenuName;
        public nint lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSG
    {
        public nint hwnd;
        public uint message;
        public nint wParam;
        public nint lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INITCOMMONCONTROLSEX
    {
        public uint dwSize;
        public uint dwICC;
    }

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint GetModuleHandleW(string? lpModuleName);

    // OS 표시 언어(LANGID). 다국어 자동 선택에 사용(.NET 컬처 대신 InvariantGlobalization 유지).
    [LibraryImport("kernel32.dll")]
    internal static partial ushort GetUserDefaultUILanguage();

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint LoadCursorW(nint hInstance, nint lpCursorName);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial ushort RegisterClassExW(in WNDCLASSEXW lpwcx);

    [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreateWindowExW(
        uint dwExStyle, string lpClassName, string? lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [LibraryImport("user32.dll")]
    internal static partial nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int MessageBoxW(nint hWnd, string lpText, string lpCaption, uint uType);

    [LibraryImport("user32.dll")]
    internal static partial int ShowWindow(nint hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    internal static partial int UpdateWindow(nint hWnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial int DestroyWindow(nint hWnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial int MoveWindow(nint hWnd, int x, int y, int nWidth, int nHeight, int bRepaint);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial int SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [LibraryImport("user32.dll")]
    internal static partial uint GetDpiForWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    internal static partial int GetMessageW(ref MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [LibraryImport("user32.dll")]
    internal static partial int TranslateMessage(in MSG lpMsg);

    [LibraryImport("user32.dll")]
    internal static partial nint DispatchMessageW(in MSG lpMsg);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial int PostMessageW(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    internal static partial void PostQuitMessage(int nExitCode);

    [LibraryImport("user32.dll")]
    internal static partial nint SendMessageW(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int SetWindowTextW(nint hWnd, string lpString);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint GetWindowLongPtrW(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint SetWindowLongPtrW(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll")]
    internal static partial int GetSystemMetrics(int nIndex);

    // 스케일된 UI 폰트 생성(High DPI). 반환 HFONT 는 사용 후 DeleteObject 필요.
    [LibraryImport("gdi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreateFontW(
        int cHeight, int cWidth, int cEscapement, int cOrientation, int cWeight,
        uint bItalic, uint bUnderline, uint bStrikeOut, uint iCharSet,
        uint iOutPrecision, uint iClipPrecision, uint iQuality, uint iPitchAndFamily,
        string pszFaceName);

    [LibraryImport("gdi32.dll")]
    internal static partial int DeleteObject(nint ho);

    [LibraryImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool InitCommonControlsEx(in INITCOMMONCONTROLSEX picce);

    // 자기 exe 에 임베드된 아이콘(ApplicationIcon)을 인덱스 0(첫 아이콘 그룹)으로 추출.
    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint ExtractIconExW(string lpszFile, int nIconIndex, out nint phiconLarge, out nint phiconSmall, uint nIcons);

    // COM: 작업 표시줄 진행률(ITaskbarList3). 소스 생성 ComWrappers 로 AOT 안전하게 사용.
    [LibraryImport("ole32.dll")]
    internal static partial int CoInitializeEx(nint pvReserved, uint dwCoInit);

    [LibraryImport("ole32.dll")]
    internal static partial int CoCreateInstance(in Guid rclsid, nint pUnkOuter, uint dwClsContext, in Guid riid, out nint ppv);
}
