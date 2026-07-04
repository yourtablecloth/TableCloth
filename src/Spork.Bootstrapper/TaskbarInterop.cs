using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Spork.Bootstrapper;

/// <summary>
/// Windows 작업 표시줄 버튼 진행률(<c>ITaskbarList3</c>). NativeAOT 는 내장 COM 이 불가하므로
/// **소스 생성 COM**(<see cref="GeneratedComInterfaceAttribute"/> + <see cref="StrategyBasedComWrappers"/>)
/// 으로 사용한다. 메서드는 vtable 순서를 지켜 SetProgressState 까지만 선언한다(이후 슬롯은 미사용).
/// 모든 메서드는 <see cref="PreserveSigAttribute"/> 로 raw HRESULT 를 받아 예외 없이 무시한다.
/// </summary>
[GeneratedComInterface]
[Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
internal partial interface ITaskbarList3
{
    // --- ITaskbarList ---
    [PreserveSig] int HrInit();
    [PreserveSig] int AddTab(nint hwnd);
    [PreserveSig] int DeleteTab(nint hwnd);
    [PreserveSig] int ActivateTab(nint hwnd);
    [PreserveSig] int SetActiveAlt(nint hwnd);
    // --- ITaskbarList2 ---
    [PreserveSig] int MarkFullscreenWindow(nint hwnd, int fFullscreen);
    // --- ITaskbarList3 (여기까지만 사용) ---
    [PreserveSig] int SetProgressValue(nint hwnd, ulong ullCompleted, ulong ullTotal);
    [PreserveSig] int SetProgressState(nint hwnd, int tbpFlags);
}

/// <summary>
/// 작업 표시줄 진행률 헬퍼. 모두 UI(STA) 스레드에서만 호출한다(WndProc/On* 핸들러). COM 이 없거나
/// 실패하면 조용히 무시(best-effort) 해 부트스트래퍼 본기능에 영향을 주지 않는다.
/// </summary>
internal static class Taskbar
{
    // TBPFLAG
    private const int TBPF_NOPROGRESS = 0x0;
    private const int TBPF_INDETERMINATE = 0x1;
    private const int TBPF_NORMAL = 0x2;
    private const int TBPF_ERROR = 0x4;
    private const int TBPF_PAUSED = 0x8;

    private static readonly Guid CLSID_TaskbarList = new("56FDF344-FD6D-11d0-958A-006097C9A090");
    private static readonly Guid IID_ITaskbarList3 = new("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf");

    private static ITaskbarList3? _tb;
    private static nint _hwnd;

    public static void Init(nint hwnd)
    {
        _hwnd = hwnd;
        try
        {
            if (Interop.CoCreateInstance(in CLSID_TaskbarList, 0, Interop.CLSCTX_INPROC_SERVER, in IID_ITaskbarList3, out nint ptr) < 0 || ptr == 0)
                return;
            var cw = new StrategyBasedComWrappers();
            _tb = (ITaskbarList3)cw.GetOrCreateObjectForComInstance(ptr, CreateObjectFlags.None);
            Marshal.Release(ptr);
            _tb.HrInit();
        }
        catch
        {
            _tb = null; // 작업 표시줄 진행률은 선택 기능. 실패해도 진행.
        }
    }

    public static void Normal() => State(TBPF_NORMAL);
    public static void Indeterminate() => State(TBPF_INDETERMINATE);
    public static void Clear() => State(TBPF_NOPROGRESS);
    public static void Error() { State(TBPF_ERROR); Value(100); }
    public static void Paused() { State(TBPF_PAUSED); Value(100); }

    public static void Value(int pct)
    {
        if (_tb is null) return;
        try { _tb.SetProgressValue(_hwnd, (ulong)Math.Clamp(pct, 0, 100), 100); }
        catch { }
    }

    private static void State(int flags)
    {
        if (_tb is null) return;
        try { _tb.SetProgressState(_hwnd, flags); }
        catch { }
    }
}
