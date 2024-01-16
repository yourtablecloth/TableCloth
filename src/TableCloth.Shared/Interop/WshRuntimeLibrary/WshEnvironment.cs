using System;
using System.Runtime.InteropServices;

namespace TableCloth.Interop.WshRuntimeLibrary
{
    [ComImport]
    [CoClass(typeof(WshEnvironmentClass))]
    [Guid("F935DC29-1CF0-11D0-ADB9-00C04FD58A0B")]
    public interface WshEnvironment : IWshEnvironment
    {
    }
}
