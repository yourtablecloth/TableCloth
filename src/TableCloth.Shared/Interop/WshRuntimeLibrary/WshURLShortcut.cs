using System;
using System.Runtime.InteropServices;

namespace TableCloth.Interop.WshRuntimeLibrary
{
    [ComImport]
    [Guid("F935DC2B-1CF0-11D0-ADB9-00C04FD58A0B")]
    [CoClass(typeof(WshURLShortcutClass))]
    public interface WshURLShortcut : IWshURLShortcut
    {
    }
}
