using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TableCloth.Interop.Shell32
{
    [ComImport]
    [Guid("866738B9-6CF2-4DE8-8767-F794EBE74F4E")]
    [TypeLibType(4176)]
    public interface IShellDispatch5 : IShellDispatch4
    {
        [DispId(1610743808)]
        new object Application
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(1610743808)]
            [return: MarshalAs(UnmanagedType.IDispatch)]
            get;
        }

        [DispId(1610743809)]
        new object Parent
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(1610743809)]
            [return: MarshalAs(UnmanagedType.IDispatch)]
            get;
        }

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743810)]
        [return: MarshalAs(UnmanagedType.Interface)]
        new Folder NameSpace([In][MarshalAs(UnmanagedType.Struct)] object vDir);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743811)]
        [return: MarshalAs(UnmanagedType.Interface)]
        new Folder BrowseForFolder([In] int Hwnd, [In][MarshalAs(UnmanagedType.BStr)] string Title, [In] int Options, [Optional][In][MarshalAs(UnmanagedType.Struct)] object RootFolder);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743812)]
        [return: MarshalAs(UnmanagedType.IDispatch)]
        new object Windows();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743813)]
        new void Open([In][MarshalAs(UnmanagedType.Struct)] object vDir);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743814)]
        new void Explore([In][MarshalAs(UnmanagedType.Struct)] object vDir);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743815)]
        new void MinimizeAll();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743816)]
        new void UndoMinimizeALL();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743817)]
        new void FileRun();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743818)]
        new void CascadeWindows();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743819)]
        new void TileVertically();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743820)]
        new void TileHorizontally();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743821)]
        new void ShutdownWindows();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743822)]
        new void Suspend();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743823)]
        new void EjectPC();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743824)]
        new void SetTime();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743825)]
        new void TrayProperties();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743826)]
        new void Help();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743827)]
        new void FindFiles();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743828)]
        new void FindComputer();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743829)]
        new void RefreshMenu();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743830)]
        new void ControlPanelItem([In][MarshalAs(UnmanagedType.BStr)] string bstrDir);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610809344)]
        new int IsRestricted([In][MarshalAs(UnmanagedType.BStr)] string Group, [In][MarshalAs(UnmanagedType.BStr)] string Restriction);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610809345)]
        new void ShellExecute([In][MarshalAs(UnmanagedType.BStr)] string File, [Optional][In][MarshalAs(UnmanagedType.Struct)] object vArgs, [Optional][In][MarshalAs(UnmanagedType.Struct)] object vDir, [Optional][In][MarshalAs(UnmanagedType.Struct)] object vOperation, [Optional][In][MarshalAs(UnmanagedType.Struct)] object vShow);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610809346)]
        new void FindPrinter([Optional][In][MarshalAs(UnmanagedType.BStr)] string Name, [Optional][In][MarshalAs(UnmanagedType.BStr)] string location, [Optional][In][MarshalAs(UnmanagedType.BStr)] string model);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610809347)]
        [return: MarshalAs(UnmanagedType.Struct)]
        new object GetSystemInformation([In][MarshalAs(UnmanagedType.BStr)] string Name);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610809348)]
        [return: MarshalAs(UnmanagedType.Struct)]
        new object ServiceStart([In][MarshalAs(UnmanagedType.BStr)] string ServiceName, [In][MarshalAs(UnmanagedType.Struct)] object Persistent);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610809349)]
        [return: MarshalAs(UnmanagedType.Struct)]
        new object ServiceStop([In][MarshalAs(UnmanagedType.BStr)] string ServiceName, [In][MarshalAs(UnmanagedType.Struct)] object Persistent);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610809350)]
        [return: MarshalAs(UnmanagedType.Struct)]
        new object IsServiceRunning([In][MarshalAs(UnmanagedType.BStr)] string ServiceName);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610809351)]
        [return: MarshalAs(UnmanagedType.Struct)]
        new object CanStartStopService([In][MarshalAs(UnmanagedType.BStr)] string ServiceName);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610809352)]
        [return: MarshalAs(UnmanagedType.Struct)]
        new object ShowBrowserBar([In][MarshalAs(UnmanagedType.BStr)] string bstrClsid, [In][MarshalAs(UnmanagedType.Struct)] object bShow);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610874880)]
        new void AddToRecent([In][MarshalAs(UnmanagedType.Struct)] object varFile, [Optional][In][MarshalAs(UnmanagedType.BStr)] string bstrCategory);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610940416)]
        new void WindowsSecurity();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610940417)]
        new void ToggleDesktop();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610940418)]
        [return: MarshalAs(UnmanagedType.Struct)]
        new object ExplorerPolicy([In][MarshalAs(UnmanagedType.BStr)] string bstrPolicyName);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610940419)]
        new bool GetSetting([In] int lSetting);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1611005952)]
        void WindowSwitcher();
    }
}
