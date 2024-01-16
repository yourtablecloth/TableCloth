using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TableCloth.Interop.WshRuntimeLibrary
{
    [ComImport]
    [DefaultMember("Item")]
    [ClassInterface((short)0)]
    [Guid("387DAFF4-DA03-44D2-B0D1-80C927C905AC")]
    public class WshCollectionClass : IWshCollection, WshCollection, IEnumerable
    {
        [DispId(2)]
        public virtual extern int length
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(2)]
            get;
        }

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(0)]
        [return: MarshalAs(UnmanagedType.Struct)]
        public virtual extern object Item([In][MarshalAs(UnmanagedType.Struct)] ref object Index);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1)]
        public virtual extern int Count();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(-4)]
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "System.Runtime.InteropServices.CustomMarshalers.EnumeratorToEnumVariantMarshaler, CustomMarshalers, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
        public virtual extern IEnumerator GetEnumerator();
    }
}
