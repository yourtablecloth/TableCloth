using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TableCloth.Interop.WshRuntimeLibrary
{
    [ComImport]
    [TypeLibType(4176)]
    [Guid("24BE5A30-EDFE-11D2-B933-00104B365C9F")]
    public interface IWshShell2 : IWshShell
    {
        [DispId(100)]
        new IWshCollection SpecialFolders
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(100)]
            [return: MarshalAs(UnmanagedType.Interface)]
            get;
        }

        [DispId(200)]
        new IWshEnvironment Environment
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(200)]
            [return: MarshalAs(UnmanagedType.Interface)]
            get;
        }

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1000)]
        new int Run([In][MarshalAs(UnmanagedType.BStr)] string Command, [Optional][In][MarshalAs(UnmanagedType.Struct)] ref object WindowStyle, [Optional][In][MarshalAs(UnmanagedType.Struct)] ref object WaitOnReturn);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1001)]
        new int Popup([In][MarshalAs(UnmanagedType.BStr)] string Text, [Optional][In][MarshalAs(UnmanagedType.Struct)] ref object SecondsToWait, [Optional][In][MarshalAs(UnmanagedType.Struct)] ref object Title, [Optional][In][MarshalAs(UnmanagedType.Struct)] ref object Type);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1002)]
        [return: MarshalAs(UnmanagedType.IDispatch)]
        new object CreateShortcut([In][MarshalAs(UnmanagedType.BStr)] string PathLink);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1006)]
        [return: MarshalAs(UnmanagedType.BStr)]
        new string ExpandEnvironmentStrings([In][MarshalAs(UnmanagedType.BStr)] string Src);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(2000)]
        [return: MarshalAs(UnmanagedType.Struct)]
        new object RegRead([In][MarshalAs(UnmanagedType.BStr)] string Name);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(2001)]
        new void RegWrite([In][MarshalAs(UnmanagedType.BStr)] string Name, [In][MarshalAs(UnmanagedType.Struct)] ref object Value, [Optional][In][MarshalAs(UnmanagedType.Struct)] ref object Type);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(2002)]
        new void RegDelete([In][MarshalAs(UnmanagedType.BStr)] string Name);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(3000)]
        bool LogEvent([In][MarshalAs(UnmanagedType.Struct)] ref object Type, [In][MarshalAs(UnmanagedType.BStr)] string Message, [In][MarshalAs(UnmanagedType.BStr)] string Target = "");

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(3010)]
        bool AppActivate([In][MarshalAs(UnmanagedType.Struct)] ref object App, [Optional][In][MarshalAs(UnmanagedType.Struct)] ref object Wait);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(3011)]
        void SendKeys([In][MarshalAs(UnmanagedType.BStr)] string Keys, [Optional][In][MarshalAs(UnmanagedType.Struct)] ref object Wait);
    }
}
