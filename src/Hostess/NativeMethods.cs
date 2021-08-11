using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Hostess
{
    [SuppressUnmanagedCodeSecurity]
    internal static class NativeMethods
    {
        public static readonly Guid DownloadFolderGuid = new Guid("{374DE290-123F-4565-9164-39C4925E467B}");

        public static string GetKnownFolderPath(Guid knownFolderGuid, KnownFolderFlags flags = KnownFolderFlags.DontVerify, bool defaultUser = false)
        {
            var result = SHGetKnownFolderPath(knownFolderGuid, (int)flags, new IntPtr(defaultUser ? -1 : 0), out var outPath);

            if (result >= 0)
            {
                var path = Marshal.PtrToStringUni(outPath);
                Marshal.FreeCoTaskMem(outPath);
                return path;
            }
            else
            {
                throw new ExternalException("Unable to retrieve the known folder path. It may not be available on this system.", result);
            }
        }

        [DllImport("shell32.dll",
            SetLastError = false,
            CharSet = CharSet.None,
            ExactSpelling = true,
            EntryPoint = nameof(SHGetKnownFolderPath),
            CallingConvention = CallingConvention.StdCall)]
        private static extern int SHGetKnownFolderPath(
            [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
            [MarshalAs(UnmanagedType.U4)] int dwFlags,
            IntPtr hToken, out IntPtr ppszPath);

        [Flags]
        public enum KnownFolderFlags : uint
        {
            SimpleIDList = 0x00000100,
            NotParentRelative = 0x00000200,
            DefaultPath = 0x00000400,
            Init = 0x00000800,
            NoAlias = 0x00001000,
            DontUnexpand = 0x00002000,
            DontVerify = 0x00004000,
            Create = 0x00008000,
            NoAppcontainerRedirection = 0x00010000,
            AliasOnly = 0x80000000
        }

        public const int SetDesktopWallpaper = 0x0014;
        public const int UpdateIniFile = 0x01;
        public const int SendWinIniChange = 0x02;

        [DllImport("user32.dll",
            SetLastError = true,
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            EntryPoint = nameof(SystemParametersInfoW),
            CallingConvention = CallingConvention.Winapi)]
        public static extern int SystemParametersInfoW(
            [MarshalAs(UnmanagedType.U4)] int uAction,
            [MarshalAs(UnmanagedType.U4)] int uParam,
            string lpvParam,
            [MarshalAs(UnmanagedType.U4)] int fuWinIni);

        [DllImport("user32.dll",
            SetLastError = false,
            CharSet = CharSet.Ansi,
            ExactSpelling = true,
            EntryPoint = nameof(UpdatePerUserSystemParameters),
            CallingConvention = CallingConvention.StdCall)]
        public static extern void UpdatePerUserSystemParameters(
            IntPtr hWnd,
            IntPtr hInstance,
            string lpszCmdLine,
            int nCmdShow);
    }
}
