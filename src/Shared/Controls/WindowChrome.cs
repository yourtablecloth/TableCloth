#nullable enable

using Avalonia.Controls;
using System;
using System.Runtime.InteropServices;

namespace TableCloth.Controls
{
    /// <summary>
    /// 이슈 #296: 모달 다이얼로그 창 크롬 조정 헬퍼. Avalonia 에는 최소화 버튼만 끄는 전용 플래그가 없어
    /// (Windows 전용 앱이므로) Win32 로 WS_MINIMIZEBOX 스타일 비트를 해제한다. 모달을 띄우는 공통 지점
    /// (DialogHost/MessageBoxWindow)에서 호출해 모든 모달 다이얼로그에 일관 적용. src/Shared 공유 링크.
    /// </summary>
    public static class WindowChrome
    {
        private const int GWL_STYLE = -16;
        private const int WS_MINIMIZEBOX = 0x00020000;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLongW(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLongW(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        /// <summary>
        /// 창이 열릴 때 최소화 버튼을 제거한다. 창 표시 전에 호출하면 Opened 시점에 적용된다.
        /// </summary>
        public static void RemoveMinimizeBox(Window? window)
        {
            if (window == null)
                return;

            window.Opened += OnOpened;
        }

        private static void OnOpened(object? sender, EventArgs e)
        {
            if (sender is not Window window)
                return;

            window.Opened -= OnOpened;

            try
            {
                var hwnd = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (hwnd == IntPtr.Zero)
                    return;

                var style = GetWindowLongW(hwnd, GWL_STYLE);
                SetWindowLongW(hwnd, GWL_STYLE, style & ~WS_MINIMIZEBOX);

                // 스타일 변경을 타이틀바(비클라이언트 영역)에 즉시 반영.
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            }
            catch
            {
                // 창 크롬 조정은 best-effort — 실패해도 기능에 영향 없음.
            }
        }
    }
}
