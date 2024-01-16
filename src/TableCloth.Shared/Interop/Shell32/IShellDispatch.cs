using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TableCloth.Interop.Shell32
{
    [ComImport]
    [TypeLibType(4176)]
    [Guid("D8F015C0-C278-11CE-A49E-444553540000")]
    public interface IShellDispatch
    {
        [DispId(1610743808)]
        object Application
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(1610743808)]
            [return: MarshalAs(UnmanagedType.IDispatch)]
            get;
        }

        [DispId(1610743809)]
        object Parent
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(1610743809)]
            [return: MarshalAs(UnmanagedType.IDispatch)]
            get;
        }

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743810)]
        [return: MarshalAs(UnmanagedType.Interface)]
        Folder NameSpace([In][MarshalAs(UnmanagedType.Struct)] object vDir);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743811)]
        [return: MarshalAs(UnmanagedType.Interface)]
        Folder BrowseForFolder([In] int Hwnd, [In][MarshalAs(UnmanagedType.BStr)] string Title, [In] int Options, [Optional][In][MarshalAs(UnmanagedType.Struct)] object RootFolder);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743812)]
        [return: MarshalAs(UnmanagedType.IDispatch)]
        object Windows();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743813)]
        void Open([In][MarshalAs(UnmanagedType.Struct)] object vDir);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743814)]
        void Explore([In][MarshalAs(UnmanagedType.Struct)] object vDir);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743815)]
        void MinimizeAll();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743816)]
        void UndoMinimizeALL();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743817)]
        void FileRun();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743818)]
        void CascadeWindows();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743819)]
        void TileVertically();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743820)]
        void TileHorizontally();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743821)]
        void ShutdownWindows();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743822)]
        void Suspend();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743823)]
        void EjectPC();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743824)]
        void SetTime();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743825)]
        void TrayProperties();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743826)]
        void Help();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743827)]
        void FindFiles();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743828)]
        void FindComputer();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743829)]
        void RefreshMenu();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743830)]
        void ControlPanelItem([In][MarshalAs(UnmanagedType.BStr)] string bstrDir);
    }
}
