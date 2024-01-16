using System;
using System.Runtime.InteropServices;

namespace TableCloth.Interop.WshRuntimeLibrary
{
    [ComImport]
    [CoClass(typeof(WshExecClass))]
    [Guid("08FED190-BE19-11D3-A28B-00104BD35090")]
    public interface WshExec : IWshExec
    {
    }
}
