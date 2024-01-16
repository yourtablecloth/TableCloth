using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TableCloth.Interop.WshRuntimeLibrary
{
    [ComImport]
    [TypeLibType(2)]
    [ClassInterface((short)0)]
    [Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8")]
    public class WshShellClass : IWshShell3, WshShell
    {
        [DispId(100)]
        public virtual extern IWshCollection SpecialFolders
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(100)]
            [return: MarshalAs(UnmanagedType.Interface)]
            get;
        }

        [DispId(200)]
        public virtual extern IWshEnvironment Environment
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(200)]
            [return: MarshalAs(UnmanagedType.Interface)]
            get;
        }

        [DispId(3013)]
        public virtual extern string CurrentDirectory
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(3013)]
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(3013)]
            [param: In]
            [param: MarshalAs(UnmanagedType.BStr)]
            set;
        }

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1000)]
        public virtual extern int Run([In][MarshalAs(UnmanagedType.BStr)] string Command, [Optional][In][MarshalAs(UnmanagedType.Struct)] ref object WindowStyle, [Optional][In][MarshalAs(UnmanagedType.Struct)] ref object WaitOnReturn);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1001)]
        public virtual extern int Popup([In][MarshalAs(UnmanagedType.BStr)] string Text, [Optional][In][MarshalAs(UnmanagedType.Struct)] ref object SecondsToWait, [Optional][In][MarshalAs(UnmanagedType.Struct)] ref object Title, [Optional][In][MarshalAs(UnmanagedType.Struct)] ref object Type);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1002)]
        [return: MarshalAs(UnmanagedType.IDispatch)]
        public virtual extern object CreateShortcut([In][MarshalAs(UnmanagedType.BStr)] string PathLink);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1006)]
        [return: MarshalAs(UnmanagedType.BStr)]
        public virtual extern string ExpandEnvironmentStrings([In][MarshalAs(UnmanagedType.BStr)] string Src);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(2000)]
        [return: MarshalAs(UnmanagedType.Struct)]
        public virtual extern object RegRead([In][MarshalAs(UnmanagedType.BStr)] string Name);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(2001)]
        public virtual extern void RegWrite([In][MarshalAs(UnmanagedType.BStr)] string Name, [In][MarshalAs(UnmanagedType.Struct)] ref object Value, [Optional][In][MarshalAs(UnmanagedType.Struct)] ref object Type);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(2002)]
        public virtual extern void RegDelete([In][MarshalAs(UnmanagedType.BStr)] string Name);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(3000)]
        public virtual extern bool LogEvent([In][MarshalAs(UnmanagedType.Struct)] ref object Type, [In][MarshalAs(UnmanagedType.BStr)] string Message, [In][MarshalAs(UnmanagedType.BStr)] string Target = "");

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(3010)]
        public virtual extern bool AppActivate([In][MarshalAs(UnmanagedType.Struct)] ref object App, [Optional][In][MarshalAs(UnmanagedType.Struct)] ref object Wait);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(3011)]
        public virtual extern void SendKeys([In][MarshalAs(UnmanagedType.BStr)] string Keys, [Optional][In][MarshalAs(UnmanagedType.Struct)] ref object Wait);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(3012)]
        [return: MarshalAs(UnmanagedType.Interface)]
        public virtual extern WshExec Exec([In][MarshalAs(UnmanagedType.BStr)] string Command);
    }
}
