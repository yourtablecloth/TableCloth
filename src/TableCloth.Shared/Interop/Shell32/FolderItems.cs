using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TableCloth.Interop.Shell32
{
    [ComImport]
    [Guid("744129E0-CBE5-11CE-8350-444553540000")]
    [TypeLibType(4160)]
    public interface FolderItems : IEnumerable
    {
        [DispId(1610743808)]
        int Count
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(1610743808)]
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

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743811)]
        [return: MarshalAs(UnmanagedType.Interface)]
        FolderItem Item([Optional][In][MarshalAs(UnmanagedType.Struct)] object index);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(-4)]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "System.Runtime.InteropServices.CustomMarshalers.EnumeratorToEnumVariantMarshaler, CustomMarshalers, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
        new IEnumerator GetEnumerator();
    }
}
