using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace TableCloth
{
    [SuppressUnmanagedCodeSecurity]
	static class NativeMethods
	{
		// https://stackoverflow.com/questions/336633/how-to-detect-windows-64-bit-platform-with-net
		public static bool InternalCheckIsWow64()
		{
			if ((Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor >= 1) ||
				Environment.OSVersion.Version.Major >= 6)
			{
				using (var p = Process.GetCurrentProcess())
				{
					if (!IsWow64Process(p.Handle, out bool retVal))
						return false;

					return retVal;
				}
			}
			else
			{
				return false;
			}
		}

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

		[DllImport("kernel32.dll",
			SetLastError = false,
			CharSet = CharSet.Unicode,
			ExactSpelling = true,
			CallingConvention = CallingConvention.Winapi,
			EntryPoint = nameof(GetPrivateProfileSectionNamesW))]
		[return: MarshalAs(UnmanagedType.U4)]
		public static extern int GetPrivateProfileSectionNamesW(
			IntPtr lpszReturnBuffer,
			[MarshalAs(UnmanagedType.U4)] int nSize,
			string lpFileName);

		[DllImport("kernel32.dll",
			SetLastError = false,
			CharSet = CharSet.Unicode,
			ExactSpelling = true,
			CallingConvention = CallingConvention.Winapi,
			EntryPoint = nameof(GetPrivateProfileStringW))]
		[return: MarshalAs(UnmanagedType.U4)]
		public static extern int GetPrivateProfileStringW(
			string lpAppName,
			string lpKeyName,
			string lpDefault,
			StringBuilder lpReturnedString,
			[MarshalAs(UnmanagedType.U4)] int nSize,
			string lpFileName);

		[DllImport("kernel32.dll",
			SetLastError = false,
			CharSet = CharSet.Unicode,
			ExactSpelling = true,
			CallingConvention = CallingConvention.Winapi,
			EntryPoint = nameof(GetPrivateProfileStringW))]
		[return: MarshalAs(UnmanagedType.U4)]
		public static extern int GetPrivateProfileStringW(
			string lpAppName,
			string lpKeyName,
			string lpDefault,
			IntPtr lpReturnedString,
			[MarshalAs(UnmanagedType.U4)] int nSize,
			string lpFileName);

		[DllImport("kernel32.dll",
			SetLastError = false,
			CharSet = CharSet.Unicode,
			ExactSpelling = true,
			CallingConvention = CallingConvention.Winapi,
			EntryPoint = nameof(GetPrivateProfileIntW))]
		[return: MarshalAs(UnmanagedType.U4)]
		public static extern int GetPrivateProfileIntW(
			string lpAppName,
			string lpKeyName,
			int lpDefault,
			string lpFileName);

		[DllImport("kernel32.dll",
			SetLastError = false,
			CharSet = CharSet.Unicode,
			ExactSpelling = true,
			CallingConvention = CallingConvention.Winapi,
			EntryPoint = nameof(GetPrivateProfileSectionW))]
		[return: MarshalAs(UnmanagedType.U4)]
		public static extern int GetPrivateProfileSectionW(
			string lpAppName,
			IntPtr lpReturnedString,
			[MarshalAs(UnmanagedType.U4)] int nSize,
			string lpFileName);
	}
}
