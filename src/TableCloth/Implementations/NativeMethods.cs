using System;
using System.Runtime.InteropServices;
using System.Security;

namespace TableCloth.Implementations
{
    [SuppressUnmanagedCodeSecurity]
    internal static class NativeMethods
    {
        public const int GWL_STYLE = -16;
        public const int WS_SYSMENU = 0x80000;

        [DllImport("kernel32.dll",
            SetLastError = true,
            CharSet = CharSet.None,
            ExactSpelling = true,
            CallingConvention = CallingConvention.Winapi,
            EntryPoint = nameof(IsWow64Process))]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWow64Process(
            [In] IntPtr hProcess,
            [Out] out bool Wow64Process);

        [DllImport("user32.dll",
            SetLastError = true,
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            CallingConvention = CallingConvention.Winapi,
            EntryPoint = nameof(GetWindowLongW))]
        public static extern int GetWindowLongW(
            [In] IntPtr hWnd,
            [In] int nIndex);

        [DllImport("user32.dll",
            SetLastError = true,
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            CallingConvention = CallingConvention.Winapi,
            EntryPoint = nameof(SetWindowLongW))]
        public static extern int SetWindowLongW(
            [In] IntPtr hWnd,
            [In] int nIndex,
            [In] int dwNewLong);
    }
}
