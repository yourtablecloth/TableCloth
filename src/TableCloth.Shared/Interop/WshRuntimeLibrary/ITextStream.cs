using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TableCloth.Interop.WshRuntimeLibrary
{
    [ComImport]
    [TypeLibType(4304)]
    [Guid("53BAD8C1-E718-11CF-893D-00A0C9054228")]
    public interface ITextStream
    {
        [DispId(10000)]
        int Line
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(10000)]
            get;
        }

        [DispId(-529)]
        int Column
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(-529)]
            get;
        }

        [DispId(10002)]
        bool AtEndOfStream
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(10002)]
            get;
        }

        [DispId(10003)]
        bool AtEndOfLine
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(10003)]
            get;
        }

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(10004)]
        [return: MarshalAs(UnmanagedType.BStr)]
        string Read([In] int Characters);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(10005)]
        [return: MarshalAs(UnmanagedType.BStr)]
        string ReadLine();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(10006)]
        [return: MarshalAs(UnmanagedType.BStr)]
        string ReadAll();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(10007)]
        void Write([In][MarshalAs(UnmanagedType.BStr)] string Text);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(10008)]
        void WriteLine([In][MarshalAs(UnmanagedType.BStr)] string Text = "");

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(10009)]
        void WriteBlankLines([In] int Lines);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(10010)]
        void Skip([In] int Characters);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(10011)]
        void SkipLine();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(10012)]
        void Close();
    }
}
