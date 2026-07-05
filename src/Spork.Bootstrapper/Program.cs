using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static Spork.Bootstrapper.Interop;

namespace Spork.Bootstrapper;

/// <summary>
/// 식탁보(무설치 Express 레인)용 소형 GUI 다운로더의 진입점. 순수 Win32/GDI 다이얼로그 하나(진행 막대 +
/// 상태/상세 라벨 + 재시도/닫기 버튼)를 띄우고, 백그라운드에서 포터블 식탁보(Spork) zip 을 받아 SHA256
/// 검증 → 압축 해제 → 실행까지 수행한다. UI 문자열은 <see cref="Loc"/> 리소스 테이블(다국어)에서 오고,
/// High DPI(PerMonitorV2)로 좌표/폰트를 스케일한다. 진행/완료/실패/취소는 <c>PostMessage</c> 로 UI
/// 스레드에 전달한다. 설계는 docs/EXPRESS_BOOTSTRAPPER_DESIGN.md 참조.
/// </summary>
internal static partial class Program
{
    private const string ClassName = "SporkBootstrapperWindow";
    private const int ErrorCanceled = 1223; // ERROR_CANCELLED: UAC 상승 취소 등.

    // 컨트롤 ID.
    private const int IDC_RETRY = 1001;
    private const int IDC_CLOSE = 1002;

    // 기준(96 DPI) 레이아웃. ApplyDpi 가 배율을 곱한다.
    private const int BaseWindowWidth = 496;
    private const int BaseWindowHeight = 236;

    // 사용자 정의 메시지(WM_APP 오프셋). 워커 스레드 → UI 스레드 통지용.
    private const uint WM_APP_PROGRESS = WM_APP + 1;      // wParam = 0..100
    private const uint WM_APP_STATUS = WM_APP + 2;        // s_statusText 반영
    private const uint WM_APP_DETAIL = WM_APP + 3;        // s_detailText 반영
    private const uint WM_APP_INDETERMINATE = WM_APP + 4; // 진행 막대 marquee 켜기
    private const uint WM_APP_DETERMINATE = WM_APP + 5;   // marquee 끄고 0..100
    private const uint WM_APP_DONE = WM_APP + 6;          // 성공: 창 닫기(실행 완료)
    private const uint WM_APP_FAIL = WM_APP + 7;          // 실패: 오류 표시 + 재시도
    private const uint WM_APP_CANCELED = WM_APP + 8;      // 실행 취소: 오류 아닌 재실행 안내

    private static nint s_hInst;
    private static nint s_classNamePtr;
    private static nint s_mainHwnd;
    private static nint s_statusLabel;
    private static nint s_progress;
    private static nint s_detailLabel;
    private static nint s_retryButton;
    private static nint s_closeButton;
    private static nint s_font;
    private static nint s_iconBig;
    private static nint s_iconSmall;

    private static BootstrapOptions s_options = null!;
    private static readonly HttpClient s_http = CreateHttpClient();

    private static readonly object s_textLock = new();
    private static string s_statusText = "";
    private static string s_detailText = "";
    private static string s_errorText = "";

    // 실행(launch) 단계 상태. 취소 후 [다시 실행]은 재다운로드 없이 실행만 재시도한다.
    private static string? s_extractedDest;
    private static string? s_launchSiteIds;
    private static bool s_launchOnlyRetry;

    private static int s_workerRunning; // Interlocked 가드(재시도 중복 방지)
    private static string? s_logPath;

    [STAThread]
    private static int Main(string[] args)
    {
        s_options = BootstrapOptions.Parse(args);
        Loc.Initialize(s_options.Lang);
        s_logPath = Path.Combine(DesktopDir(), "spork-bootstrap.log");
        Log($"bootstrapper start (arch={RuntimeInformation.ProcessArchitecture}, lang={(s_options.Lang ?? "auto")}, " +
            $"template={(s_options.ZipUrlTemplate ?? "<none>")}, siteIds='{s_options.SiteIds}')");

        CoInitializeEx(0, COINIT_APARTMENTTHREADED); // 작업 표시줄 COM 용 STA (STAThread 보조, best-effort)

        s_hInst = GetModuleHandleW(null);
        InitCommonControls();
        LoadAppIcons();

        RegisterWindowClass();
        CreateMainWindow();

        ShowWindow(s_mainHwnd, SW_SHOWNORMAL);
        UpdateWindow(s_mainHwnd);

        StartWorker();

        // 메시지 루프.
        MSG msg = default;
        int r;
        while ((r = GetMessageW(ref msg, 0, 0, 0)) != 0)
        {
            if (r == -1)
                break; // GetMessage 오류.
            TranslateMessage(in msg);
            DispatchMessageW(in msg);
        }
        Log("bootstrapper exit");
        return 0;
    }

    // ------------------------------------------------------------------
    // Win32 창/컨트롤
    // ------------------------------------------------------------------

    private static unsafe void InitCommonControls()
    {
        var icc = new INITCOMMONCONTROLSEX
        {
            dwSize = (uint)sizeof(INITCOMMONCONTROLSEX),
            dwICC = ICC_PROGRESS_CLASS | ICC_STANDARD_CLASSES,
        };
        InitCommonControlsEx(in icc);
    }

    private static unsafe void RegisterWindowClass()
    {
        s_classNamePtr = Marshal.StringToHGlobalUni(ClassName);
        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)sizeof(WNDCLASSEXW),
            style = CS_HREDRAW | CS_VREDRAW,
            lpfnWndProc = (nint)(delegate* unmanaged[Stdcall]<nint, uint, nint, nint, nint>)&WndProc,
            hInstance = s_hInst,
            hIcon = s_iconBig,
            hIconSm = s_iconSmall,
            hCursor = LoadCursorW(0, IDC_ARROW),
            hbrBackground = COLOR_BTNFACE + 1,
            lpszClassName = s_classNamePtr,
        };
        if (RegisterClassExW(in wc) == 0)
            throw new InvalidOperationException($"RegisterClassExW 실패 (err={Marshal.GetLastPInvokeError()})");
    }

    // TableCloth 앱 아이콘(ApplicationIcon 으로 exe 에 임베드됨)을 자기 exe 에서 추출.
    private static void LoadAppIcons()
    {
        try
        {
            string exe = Environment.ProcessPath ?? "";
            if (exe.Length > 0)
                ExtractIconExW(exe, 0, out s_iconBig, out s_iconSmall, 1);
        }
        catch
        {
            // 아이콘 없으면 기본 아이콘 사용. 무시.
        }
    }

    private static void CreateMainWindow()
    {
        int x = (GetSystemMetrics(SM_CXSCREEN) - BaseWindowWidth) / 2;
        int y = (GetSystemMetrics(SM_CYSCREEN) - BaseWindowHeight) / 2;
        if (x < 0) x = CW_USEDEFAULT;
        if (y < 0) y = CW_USEDEFAULT;

        // 최소화 버튼 없음: WS_MINIMIZEBOX 미포함. WS_SYSMENU 로 닫기 버튼만 남긴다.
        s_mainHwnd = CreateWindowExW(
            0, ClassName, Loc.S.WindowTitle,
            WS_CAPTION | WS_SYSMENU,
            x, y, BaseWindowWidth, BaseWindowHeight,
            0, 0, s_hInst, 0);
        if (s_mainHwnd == 0)
            throw new InvalidOperationException($"CreateWindowExW 실패 (err={Marshal.GetLastPInvokeError()})");
    }

    private static void OnCreate(nint hWnd)
    {
        // 기준(96 DPI) 좌표로 생성. 폰트/좌표는 곧바로 ApplyDpi 가 실제 DPI 로 스케일한다.
        s_statusLabel = CreateChild("STATIC", Loc.S.Preparing, SS_LEFT, 16, 16, 448, 44, hWnd, 0);
        s_progress = CreateChild(ProgressClass, null, PBS_SMOOTH, 16, 70, 448, 22, hWnd, 0);
        s_detailLabel = CreateChild("STATIC", "", SS_LEFT, 16, 100, 448, 20, hWnd, 0);
        // 재시도/다시실행 버튼은 초기에 숨김(WS_VISIBLE 미지정). 실패/취소 시에만 노출.
        s_retryButton = CreateChild("BUTTON", Loc.S.RetryButton, BS_PUSHBUTTON | WS_TABSTOP, 272, 136, 90, 30, hWnd, IDC_RETRY, visible: false);
        s_closeButton = CreateChild("BUTTON", Loc.S.CloseButton, BS_DEFPUSHBUTTON | WS_TABSTOP, 374, 136, 90, 30, hWnd, IDC_CLOSE);

        SendMessageW(s_progress, PBM_SETRANGE32, 0, 100);

        // 창/작업표시줄 아이콘(제목표시줄 = small, 작업표시줄 = big).
        if (s_iconBig != 0) SendMessageW(hWnd, WM_SETICON, ICON_BIG, s_iconBig);
        if (s_iconSmall != 0) SendMessageW(hWnd, WM_SETICON, ICON_SMALL, s_iconSmall);

        // 작업 표시줄 진행률 COM 초기화(UI/STA 스레드).
        Taskbar.Init(hWnd);

        uint dpi = GetDpiForWindow(hWnd);
        if (dpi == 0) dpi = 96;
        ApplyDpi(dpi);
        ResizeWindowForDpi(hWnd, dpi);
    }

    private static nint CreateChild(string cls, string? text, uint style, int x, int y, int w, int h, nint parent, int id, bool visible = true)
    {
        uint fullStyle = WS_CHILD | style | (visible ? WS_VISIBLE : 0u);
        nint ctrl = CreateWindowExW(0, cls, text, fullStyle, x, y, w, h, parent, id, s_hInst, 0);
        if (ctrl == 0)
        {
            // WM_CREATE(네이티브 WndProc) 안에서 throw 하면 AOT 경계를 넘어 위험하므로, 실패는
            // 로그로 남기고 진행한다. Win32 는 이후 SendMessage/MoveWindow 의 NULL hwnd 를 무해하게 무시.
            Log($"CreateWindowExW('{cls}') 실패 (err={Marshal.GetLastPInvokeError()}) — 해당 컨트롤 없이 진행.");
        }
        return ctrl;
    }

    // ------------------------------------------------------------------
    // High DPI (PerMonitorV2): 폰트/자식 좌표를 실제 DPI 로 스케일. 모니터 이동 시 WM_DPICHANGED 로 재적용.
    // ------------------------------------------------------------------

    private static void ApplyDpi(uint dpi)
    {
        double s = dpi / 96.0;

        // DPI 스케일된 UI 폰트 재생성 후 모든 컨트롤에 적용.
        if (s_font != 0) { DeleteObject(s_font); s_font = 0; }
        s_font = CreateScaledFont(dpi);
        SetFont(s_statusLabel);
        SetFont(s_progress);
        SetFont(s_detailLabel);
        SetFont(s_retryButton);
        SetFont(s_closeButton);

        MoveChild(s_statusLabel, 16, 16, 448, 44, s);
        MoveChild(s_progress, 16, 70, 448, 22, s);
        MoveChild(s_detailLabel, 16, 100, 448, 20, s);
        MoveChild(s_retryButton, 272, 136, 90, 30, s);
        MoveChild(s_closeButton, 374, 136, 90, 30, s);
    }

    private static void SetFont(nint ctrl)
    {
        if (ctrl != 0 && s_font != 0)
            SendMessageW(ctrl, WM_SETFONT, s_font, 1);
    }

    private static void MoveChild(nint ctrl, int x, int y, int w, int h, double scale)
    {
        if (ctrl != 0)
            MoveWindow(ctrl, (int)(x * scale), (int)(y * scale), (int)(w * scale), (int)(h * scale), 1);
    }

    private static nint CreateScaledFont(uint dpi)
    {
        int height = -(int)(9 * dpi / 72); // 9pt at target DPI
        return CreateFontW(height, 0, 0, 0, FW_NORMAL, 0, 0, 0, DEFAULT_CHARSET, 0, 0, CLEARTYPE_QUALITY, 0, Loc.S.FontFamily);
    }

    private static void ResizeWindowForDpi(nint hWnd, uint dpi)
    {
        double s = dpi / 96.0;
        int ow = (int)(BaseWindowWidth * s);
        int oh = (int)(BaseWindowHeight * s);
        int x = (GetSystemMetrics(SM_CXSCREEN) - ow) / 2;
        int y = (GetSystemMetrics(SM_CYSCREEN) - oh) / 2;
        if (x < 0) x = 0;
        if (y < 0) y = 0;
        SetWindowPos(hWnd, 0, x, y, ow, oh, SWP_NOZORDER);
    }

    private static unsafe RECT ReadRect(nint p) => *(RECT*)p;

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case WM_CREATE:
                OnCreate(hWnd);
                return 0;

            case WM_COMMAND:
                int id = (int)(wParam & 0xFFFF);
                if (id == IDC_CLOSE)
                    DestroyWindow(hWnd);
                else if (id == IDC_RETRY)
                    OnRetry();
                return 0;

            case WM_DPICHANGED:
            {
                uint newDpi = (uint)(wParam & 0xFFFF);
                RECT r = ReadRect(lParam); // Windows 가 제안한 새 창 사각형.
                SetWindowPos(hWnd, 0, r.left, r.top, r.right - r.left, r.bottom - r.top, SWP_NOZORDER | SWP_NOACTIVATE);
                ApplyDpi(newDpi);
                return 0;
            }

            case WM_APP_PROGRESS:
                SendMessageW(s_progress, PBM_SETPOS, wParam, 0);
                Taskbar.Value((int)wParam);
                return 0;

            case WM_APP_STATUS:
                SetWindowTextW(s_statusLabel, GetText(ref s_statusText));
                return 0;

            case WM_APP_DETAIL:
                SetWindowTextW(s_detailLabel, GetText(ref s_detailText));
                return 0;

            case WM_APP_INDETERMINATE:
                SetMarquee(true);
                Taskbar.Indeterminate();
                return 0;

            case WM_APP_DETERMINATE:
                SetMarquee(false);
                Taskbar.Normal();
                return 0;

            case WM_APP_DONE:
                Taskbar.Clear();
                DestroyWindow(hWnd);
                return 0;

            case WM_APP_FAIL:
                OnFail();
                return 0;

            case WM_APP_CANCELED:
                OnCanceled();
                return 0;

            case WM_CLOSE:
                DestroyWindow(hWnd);
                return 0;

            case WM_DESTROY:
                // 종료 정리(누수 방지): 마지막 폰트/클래스명 핸들 해제. DPI 변경 중 이전 폰트는
                // ApplyDpi 에서 이미 DeleteObject 되므로 여기선 마지막 것만 정리한다.
                if (s_font != 0) { DeleteObject(s_font); s_font = 0; }
                if (s_classNamePtr != 0) { Marshal.FreeHGlobal(s_classNamePtr); s_classNamePtr = 0; }
                PostQuitMessage(0);
                return 0;
        }
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private static void OnFail()
    {
        SetMarquee(false);
        SendMessageW(s_progress, PBM_SETPOS, 0, 0);
        SetWindowTextW(s_statusLabel, Loc.S.ErrorTitle);
        SetWindowTextW(s_detailLabel, GetText(ref s_errorText));
        SetWindowTextW(s_retryButton, Loc.S.RetryButton);
        s_launchOnlyRetry = false;
        Taskbar.Error();
        ShowWindow(s_retryButton, SW_SHOW);
    }

    // 실행 취소(UAC 취소 등): 오류가 아니라, 준비된 식탁보를 다시 실행하도록 안내한다.
    private static void OnCanceled()
    {
        SetMarquee(false);
        SendMessageW(s_progress, PBM_SETPOS, 100, 0);
        SetWindowTextW(s_statusLabel, Loc.S.LaunchCanceledTitle);
        SetWindowTextW(s_detailLabel, Loc.S.LaunchCanceledDetail);
        SetWindowTextW(s_retryButton, Loc.S.ReRunButton);
        s_launchOnlyRetry = true;
        Taskbar.Paused();
        ShowWindow(s_retryButton, SW_SHOW);
    }

    private static void OnRetry()
    {
        ShowWindow(s_retryButton, SW_HIDE);
        SetWindowTextW(s_detailLabel, "");
        Taskbar.Indeterminate();
        if (s_launchOnlyRetry)
        {
            // 재다운로드 없이 실행만 재시도.
            SetStatus(Loc.S.Launching);
            StartLaunchRetry();
        }
        else
        {
            SetStatus(Loc.S.Retrying);
            StartWorker();
        }
    }

    private static void SetMarquee(bool on)
    {
        nint style = GetWindowLongPtrW(s_progress, GWL_STYLE);
        if (on)
        {
            SetWindowLongPtrW(s_progress, GWL_STYLE, style | (nint)PBS_MARQUEE);
            SendMessageW(s_progress, PBM_SETMARQUEE, 1, 30);
        }
        else
        {
            SendMessageW(s_progress, PBM_SETMARQUEE, 0, 0);
            SetWindowLongPtrW(s_progress, GWL_STYLE, style & ~(nint)PBS_MARQUEE);
            SendMessageW(s_progress, PBM_SETRANGE32, 0, 100);
        }
    }

    // ------------------------------------------------------------------
    // 워커: 다운로드 → 검증 → 해제 → 실행
    // ------------------------------------------------------------------

    private static void StartWorker()
    {
        if (Interlocked.CompareExchange(ref s_workerRunning, 1, 0) != 0)
            return; // 이미 실행 중.
        _ = Task.Run(RunWorkerAsync);
    }

    private static async Task RunWorkerAsync()
    {
        try
        {
            string arch = ArchString();
            string dest = s_options.Dest ?? Path.Combine(DesktopDir(), "Spork");
            string zipPath = Path.Combine(Path.GetTempPath(), "Spork_Portable.zip");

            await DownloadSporkAsync(arch, zipPath);

            string? expected = s_options.Sha256For(arch);
            if (!string.IsNullOrEmpty(expected))
            {
                SetStatus(Loc.S.Verifying);
                SetIndeterminate();
                VerifySha256(zipPath, expected);
            }

            SetStatus(Loc.S.Extracting);
            SetIndeterminate();
            ExtractZip(zipPath, dest);

            s_extractedDest = dest;
            s_launchSiteIds = s_options.SiteIds;

            SetStatus(Loc.S.Launching);
            TryLaunch();
        }
        catch (Exception ex)
        {
            Log($"FAIL: {ex}");
            SetError(ex.Message);
            Post(WM_APP_FAIL);
        }
        finally
        {
            Interlocked.Exchange(ref s_workerRunning, 0);
        }
    }

    // 재다운로드 없이 실행만 재시도(취소 후 [다시 실행]).
    private static void StartLaunchRetry()
    {
        if (Interlocked.CompareExchange(ref s_workerRunning, 1, 0) != 0)
            return;
        _ = Task.Run(() =>
        {
            try { TryLaunch(); }
            finally { Interlocked.Exchange(ref s_workerRunning, 0); }
        });
    }

    // 실행 시도. 취소(ERROR_CANCELLED 1223)는 오류가 아니라 재실행 안내로 분기한다.
    private static void TryLaunch()
    {
        try
        {
            LaunchSpork(s_extractedDest!, s_launchSiteIds);
            Log("done — launched");
            Post(WM_APP_DONE);
        }
        catch (Win32Exception w) when (w.NativeErrorCode == ErrorCanceled)
        {
            Log("launch canceled by user (1223) — showing re-run guide");
            Post(WM_APP_CANCELED);
        }
        catch (Exception ex)
        {
            Log($"launch FAIL: {ex}");
            SetError(ex.Message);
            Post(WM_APP_FAIL);
        }
    }

    /// <summary>
    /// Spork 포터블 zip 을 받는다. 우선순위: (1) 인자 템플릿(Express 레인) → (2) 고정 URL
    /// (GitHub latest/download 버전프리 별칭, 런처 기본값) → (3) GitHub API 폴백(버전드 자산 매칭).
    /// </summary>
    private static async Task DownloadSporkAsync(string arch, string zipPath)
    {
        // (1) 명시적 인자 템플릿(웹앱/MCP/Express 레인).
        if (!string.IsNullOrEmpty(s_options.ZipUrlTemplate))
        {
            SetStatus(Loc.S.Downloading);
            await DownloadWithProgressAsync(s_options.ZipUrlTemplate.Replace("{arch}", arch), zipPath);
            return;
        }

        // (2) 고정 URL: 항상 최신 릴리스의 버전프리 별칭을 가리킨다. HttpClient 가 리다이렉트를 따라간다.
        string fixedUrl = $"https://github.com/{s_options.GithubRepo}/releases/latest/download/Spork_{arch}_Portable.zip";
        try
        {
            SetStatus(Loc.S.Downloading);
            await DownloadWithProgressAsync(fixedUrl, zipPath);
            return;
        }
        catch (Exception ex)
        {
            // 별칭이 아직 없는(구) 릴리스 등: API 폴백으로 버전드 자산을 찾는다.
            Log($"fixed URL failed ({ex.GetType().Name}: {ex.Message}); GitHub API 폴백");
        }

        // (3) GitHub API 폴백.
        SetStatus(Loc.S.CheckingLatest);
        SetIndeterminate();
        string apiUrl = await ResolveViaGitHubApiAsync(arch);
        SetStatus(Loc.S.Downloading);
        await DownloadWithProgressAsync(apiUrl, zipPath);
    }

    private static async Task<string> ResolveViaGitHubApiAsync(string arch)
    {
        string repo = s_options.GithubRepo;
        Log($"resolving latest release from GitHub API ({repo})");

        using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repo}/releases/latest");
        req.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var resp = await s_http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync();
        var release = await JsonSerializer.DeserializeAsync(stream, BootstrapJsonContext.Default.GitHubRelease);

        string suffix = $"_{arch}_Portable.zip";
        var asset = release?.Assets?.FirstOrDefault(a =>
            a.Name is not null &&
            a.Name.StartsWith("Spork_", StringComparison.OrdinalIgnoreCase) &&
            a.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

        if (asset?.BrowserDownloadUrl is null)
            throw new InvalidOperationException(string.Format(Loc.S.ErrAssetNotFoundFormat, $"Spork_*{suffix}"));

        Log($"resolved asset: {asset.Name} -> {asset.BrowserDownloadUrl}");
        return asset.BrowserDownloadUrl;
    }

    private static async Task DownloadWithProgressAsync(string url, string zipPath)
    {
        Log($"download: {url}");
        using var resp = await s_http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();

        long? total = resp.Content.Headers.ContentLength;
        if (total is > 0)
            SetDeterminate();
        else
            SetIndeterminate();

        await using var input = await resp.Content.ReadAsStreamAsync();
        await using var output = File.Create(zipPath);

        var buffer = new byte[81920];
        long read = 0;
        int lastPct = -1;
        int n;
        while ((n = await input.ReadAsync(buffer)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, n));
            read += n;

            if (total is > 0)
            {
                // Content-Length 불일치로 추가 데이터가 오면 100 을 넘을 수 있어 상한 클램프.
                int pct = (int)Math.Min(100, read * 100 / total.Value);
                if (pct != lastPct)
                {
                    lastPct = pct;
                    Post(WM_APP_PROGRESS, pct);
                    SetDetail(string.Format(Loc.S.DownloadProgressFormat, Mb(read), Mb(total.Value), pct));
                }
            }
            else
            {
                SetDetail(string.Format(Loc.S.DownloadIndeterminateFormat, Mb(read)));
            }
        }
        Log($"downloaded {read} bytes -> {zipPath}");
    }

    private static void VerifySha256(string path, string expectedHex)
    {
        using var stream = File.OpenRead(path);
        byte[] hash = SHA256.HashData(stream);
        string actual = Convert.ToHexString(hash);
        if (!string.Equals(actual, expectedHex.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            Log($"sha256 mismatch — expected {expectedHex.Trim()}, actual {actual}");
            throw new InvalidOperationException(Loc.S.ErrSha256);
        }
        Log("sha256 verified");
    }

    private static void ExtractZip(string zipPath, string dest)
    {
        if (Directory.Exists(dest))
        {
            // 파괴적 삭제 방지: dest 가 비어있거나 이전 Spork 추출본(Spork.exe 존재)일 때만 재귀 삭제한다.
            // --dest 오입력/주입으로 사용자의 임의 폴더(Desktop, Documents 등)가 통째로 삭제되는 것을 막는다.
            bool isEmpty = !Directory.EnumerateFileSystemEntries(dest).Any();
            bool looksLikeSpork = Directory.EnumerateFiles(dest, "Spork.exe", SearchOption.AllDirectories).Any();
            if (!isEmpty && !looksLikeSpork)
                throw new InvalidOperationException(string.Format(Loc.S.ErrUnsafeDest, dest));
            Directory.Delete(dest, recursive: true);
        }
        Directory.CreateDirectory(dest);
        ZipFile.ExtractToDirectory(zipPath, dest, overwriteFiles: true);
        Log($"extracted -> {dest}");
    }

    private static void LaunchSpork(string dest, string? siteIds)
    {
        string exe = Path.Combine(dest, "Spork.exe");
        if (!File.Exists(exe))
        {
            // 포터블 레이아웃이 하위 폴더를 쓰는 경우 대비 재귀 탐색.
            exe = Directory.EnumerateFiles(dest, "Spork.exe", SearchOption.AllDirectories).FirstOrDefault()
                  ?? throw new FileNotFoundException(Loc.S.ErrSporkNotFound);
        }

        var psi = new ProcessStartInfo(exe)
        {
            // Spork.exe 는 requireAdministrator 매니페스트라 권한 상승이 필요하다. ShellExecute 경로
            // (UseShellExecute=true)로 실행해야 상승이 일어난다. UseShellExecute=false 는 상승 대상 실행 시
            // Win32 error 740(ELEVATION_REQUIRED)로 실패한다. 참조 spork-bootstrap.ps1 의 Start-Process 와
            // 동일한 동작: 샌드박스의 이미 상승된 LogonCommand 트리에선 프롬프트 없이, 일반 호스트(패턴 A)에선
            // UAC 1회. 사용자가 그 UAC 를 취소하면 ERROR_CANCELLED(1223) 이 오고, 이는 오류가 아니라
            // TryLaunch 에서 "다시 실행" 안내로 분기한다. (ArgumentList 는 UseShellExecute=true 에서도 전달됨.)
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(exe),
        };
        if (!string.IsNullOrWhiteSpace(siteIds))
        {
            foreach (var siteId in siteIds.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                psi.ArgumentList.Add(siteId);
        }
        Process.Start(psi);
        Log($"launched: {exe} (siteIds='{siteIds}')");
    }

    // ------------------------------------------------------------------
    // 스레드 간 상태 전달 헬퍼
    // ------------------------------------------------------------------

    private static void Post(uint msg, int wParam = 0) => PostMessageW(s_mainHwnd, msg, wParam, 0);

    private static void SetStatus(string s)
    {
        lock (s_textLock) s_statusText = s;
        Post(WM_APP_STATUS);
    }

    private static void SetDetail(string s)
    {
        lock (s_textLock) s_detailText = s;
        Post(WM_APP_DETAIL);
    }

    private static void SetError(string s)
    {
        lock (s_textLock) s_errorText = s;
    }

    private static void SetIndeterminate() => Post(WM_APP_INDETERMINATE);
    private static void SetDeterminate() => Post(WM_APP_DETERMINATE);

    private static string GetText(ref string field)
    {
        lock (s_textLock) return field;
    }

    // ------------------------------------------------------------------
    // 유틸
    // ------------------------------------------------------------------

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.Add("User-Agent", "Spork.Bootstrapper");
        return http;
    }

    internal static string ArchString() => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 => "x64",
        Architecture.Arm64 => "arm64",
        var other => throw new PlatformNotSupportedException(string.Format(Loc.S.ErrUnsupportedArchFormat, other)),
    };

    private static string Mb(long bytes) => (bytes / 1048576.0).ToString("0.0");

    private static string DesktopDir()
    {
        string d = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        return string.IsNullOrEmpty(d) ? Path.GetTempPath() : d;
    }

    private static void Log(string message)
    {
        if (s_logPath is null)
            return;
        try
        {
            File.AppendAllText(s_logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}", Encoding.UTF8);
        }
        catch
        {
            // 로그는 best-effort. 실패해도 무시.
        }
    }
}
