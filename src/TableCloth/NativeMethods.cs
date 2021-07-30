using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;

namespace TableCloth
{
    [SuppressUnmanagedCodeSecurity]
    internal static class NativeMethods
    {
        // https://stackoverflow.com/questions/336633/how-to-detect-windows-64-bit-platform-with-net
        public static bool InternalCheckIsWow64()
            => ((Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor >= 1) || Environment.OSVersion.Version.Major >= 6) && IsWow64Process(Process.GetCurrentProcess().Handle, out var retVal) && retVal;

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
    }
}
