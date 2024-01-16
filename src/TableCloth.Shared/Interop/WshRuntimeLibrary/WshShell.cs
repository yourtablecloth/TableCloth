using System;
using System.Runtime.InteropServices;

namespace TableCloth.Interop.WshRuntimeLibrary
{
    [ComImport]
    [Guid("41904400-BE18-11D3-A28B-00104BD35090")]
    [CoClass(typeof(WshShellClass))]
    public interface WshShell : IWshShell3
    {
    }
}
