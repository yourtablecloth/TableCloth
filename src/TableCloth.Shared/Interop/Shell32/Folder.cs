using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TableCloth.Interop.Shell32
{
    [ComImport]
    [Guid("BBCBDE60-C3FF-11CE-8350-444553540000")]
    [TypeLibType(4160)]
    [DefaultMember("Title")]
    public interface Folder
    {
        [DispId(0)]
        string Title
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(0)]
            [return: MarshalAs(UnmanagedType.BStr)]
            get;
        }

        [DispId(1610743809)]
        object Application
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(1610743809)]
            [return: MarshalAs(UnmanagedType.IDispatch)]
            get;
        }

        [DispId(1610743810)]
        object Parent
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(1610743810)]
            [return: MarshalAs(UnmanagedType.IDispatch)]
            get;
        }

        [DispId(1610743811)]
        Folder ParentFolder
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(1610743811)]
            [return: MarshalAs(UnmanagedType.Interface)]
            get;
        }

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743812)]
        [return: MarshalAs(UnmanagedType.Interface)]
        FolderItems Items();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743813)]
        [return: MarshalAs(UnmanagedType.Interface)]
        FolderItem ParseName([In][MarshalAs(UnmanagedType.BStr)] string bName);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743814)]
        void NewFolder([In][MarshalAs(UnmanagedType.BStr)] string bName, [Optional][In][MarshalAs(UnmanagedType.Struct)] object vOptions);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743815)]
        void MoveHere([In][MarshalAs(UnmanagedType.Struct)] object vItem, [Optional][In][MarshalAs(UnmanagedType.Struct)] object vOptions);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743816)]
        void CopyHere([In][MarshalAs(UnmanagedType.Struct)] object vItem, [Optional][In][MarshalAs(UnmanagedType.Struct)] object vOptions);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743817)]
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetDetailsOf([In][MarshalAs(UnmanagedType.Struct)] object vItem, [In] int iColumn);
    }
}
