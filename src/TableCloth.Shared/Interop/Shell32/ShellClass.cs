using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TableCloth.Interop.Shell32
{
    [ComImport]
    [TypeLibType(2)]
    [ClassInterface((short)0)]
    [Guid("13709620-C279-11CE-A49E-444553540000")]
    public class ShellClass : IShellDispatch6, Shell
    {
        [DispId(1610743808)]
        public virtual extern object Application
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(1610743808)]
            [return: MarshalAs(UnmanagedType.IDispatch)]
            get;
        }

        [DispId(1610743809)]
        public virtual extern object Parent
        {
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
            [DispId(1610743809)]
            [return: MarshalAs(UnmanagedType.IDispatch)]
            get;
        }

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743810)]
        [return: MarshalAs(UnmanagedType.Interface)]
        public virtual extern Folder NameSpace([In][MarshalAs(UnmanagedType.Struct)] object vDir);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743811)]
        [return: MarshalAs(UnmanagedType.Interface)]
        public virtual extern Folder BrowseForFolder([In] int Hwnd, [In][MarshalAs(UnmanagedType.BStr)] string Title, [In] int Options, [Optional][In][MarshalAs(UnmanagedType.Struct)] object RootFolder);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743812)]
        [return: MarshalAs(UnmanagedType.IDispatch)]
        public virtual extern object Windows();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743813)]
        public virtual extern void Open([In][MarshalAs(UnmanagedType.Struct)] object vDir);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743814)]
        public virtual extern void Explore([In][MarshalAs(UnmanagedType.Struct)] object vDir);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743815)]
        public virtual extern void MinimizeAll();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743816)]
        public virtual extern void UndoMinimizeALL();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743817)]
        public virtual extern void FileRun();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743818)]
        public virtual extern void CascadeWindows();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743819)]
        public virtual extern void TileVertically();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743820)]
        public virtual extern void TileHorizontally();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743821)]
        public virtual extern void ShutdownWindows();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743822)]
        public virtual extern void Suspend();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743823)]
        public virtual extern void EjectPC();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743824)]
        public virtual extern void SetTime();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743825)]
        public virtual extern void TrayProperties();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743826)]
        public virtual extern void Help();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743827)]
        public virtual extern void FindFiles();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743828)]
        public virtual extern void FindComputer();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743829)]
        public virtual extern void RefreshMenu();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610743830)]
        public virtual extern void ControlPanelItem([In][MarshalAs(UnmanagedType.BStr)] string bstrDir);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610809344)]
        public virtual extern int IsRestricted([In][MarshalAs(UnmanagedType.BStr)] string Group, [In][MarshalAs(UnmanagedType.BStr)] string Restriction);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610809345)]
        public virtual extern void ShellExecute([In][MarshalAs(UnmanagedType.BStr)] string File, [Optional][In][MarshalAs(UnmanagedType.Struct)] object vArgs, [Optional][In][MarshalAs(UnmanagedType.Struct)] object vDir, [Optional][In][MarshalAs(UnmanagedType.Struct)] object vOperation, [Optional][In][MarshalAs(UnmanagedType.Struct)] object vShow);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610809346)]
        public virtual extern void FindPrinter([Optional][In][MarshalAs(UnmanagedType.BStr)] string Name, [Optional][In][MarshalAs(UnmanagedType.BStr)] string location, [Optional][In][MarshalAs(UnmanagedType.BStr)] string model);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610809347)]
        [return: MarshalAs(UnmanagedType.Struct)]
        public virtual extern object GetSystemInformation([In][MarshalAs(UnmanagedType.BStr)] string Name);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610809348)]
        [return: MarshalAs(UnmanagedType.Struct)]
        public virtual extern object ServiceStart([In][MarshalAs(UnmanagedType.BStr)] string ServiceName, [In][MarshalAs(UnmanagedType.Struct)] object Persistent);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610809349)]
        [return: MarshalAs(UnmanagedType.Struct)]
        public virtual extern object ServiceStop([In][MarshalAs(UnmanagedType.BStr)] string ServiceName, [In][MarshalAs(UnmanagedType.Struct)] object Persistent);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610809350)]
        [return: MarshalAs(UnmanagedType.Struct)]
        public virtual extern object IsServiceRunning([In][MarshalAs(UnmanagedType.BStr)] string ServiceName);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610809351)]
        [return: MarshalAs(UnmanagedType.Struct)]
        public virtual extern object CanStartStopService([In][MarshalAs(UnmanagedType.BStr)] string ServiceName);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610809352)]
        [return: MarshalAs(UnmanagedType.Struct)]
        public virtual extern object ShowBrowserBar([In][MarshalAs(UnmanagedType.BStr)] string bstrClsid, [In][MarshalAs(UnmanagedType.Struct)] object bShow);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610874880)]
        public virtual extern void AddToRecent([In][MarshalAs(UnmanagedType.Struct)] object varFile, [Optional][In][MarshalAs(UnmanagedType.BStr)] string bstrCategory);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610940416)]
        public virtual extern void WindowsSecurity();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610940417)]
        public virtual extern void ToggleDesktop();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610940418)]
        [return: MarshalAs(UnmanagedType.Struct)]
        public virtual extern object ExplorerPolicy([In][MarshalAs(UnmanagedType.BStr)] string bstrPolicyName);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1610940419)]
        public virtual extern bool GetSetting([In] int lSetting);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1611005952)]
        public virtual extern void WindowSwitcher();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        [DispId(1611071488)]
        public virtual extern void SearchCommand();
    }
}
