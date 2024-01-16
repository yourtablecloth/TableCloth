using System.Runtime.InteropServices;

namespace TableCloth.Interop.Shell32
{
    [ComImport]
    [Guid("286E6F1B-7113-4355-9562-96B7E9D64C54")]
    [CoClass(typeof(ShellClass))]
    public interface Shell : IShellDispatch6
    {
    }
}
