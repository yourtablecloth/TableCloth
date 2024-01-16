using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TableCloth.Interop.WshRuntimeLibrary
{
    [ComImport]
    [Guid("50E13488-6F1E-4450-96B0-873755403955")]
    [ClassInterface((short)0)]
    [DefaultMember("FullName")]
    public class WshURLShortcutClass : IWshURLShortcut, WshURLShortcut
    {
        [DispId(0)]
        public virtual extern string FullName
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(0)]
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(1005)]
        public virtual extern string TargetPath
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(1005)]
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(1005)]
            [param: In]
            [param: MarshalAs(UnmanagedType.BStr)]
            set;
        }

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [TypeLibFunc(64)]
        [DispId(2000)]
        public virtual extern void Load([In][MarshalAs(UnmanagedType.BStr)] string PathLink);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(2001)]
        public virtual extern void Save();
    }
}
