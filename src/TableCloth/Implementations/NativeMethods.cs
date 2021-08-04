using System;
using System.Runtime.InteropServices;
using System.Security;

namespace TableCloth.Implementations
{
    [SuppressUnmanagedCodeSecurity]
    internal static class NativeMethods
    {
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
