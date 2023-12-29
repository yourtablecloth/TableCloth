using System;
using System.Runtime.InteropServices;
using System.Security;

namespace TableCloth
{
    [SuppressUnmanagedCodeSecurity]
    internal static class NativeMethods
    {
        // https://stackoverflow.com/questions/51334674/how-to-detect-windows-10-light-dark-mode-in-win32-application
        public const int WM_SETTINGCHANGE = 0x001A;

        public const int GWL_STYLE = -16;
        public const int WS_SYSMENU = 0x80000;

        [DllImport("kernel32.dll",
            SetLastError = true,
            CharSet = CharSet.None,
            ExactSpelling = true,
            CallingConvention = CallingConvention.Winapi,
            EntryPoint = nameof(IsWow64Process))]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWow64Process(
            [In] IntPtr hProcess,
            [Out] out bool Wow64Process);

        [DllImport("user32.dll",
            SetLastError = true,
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            CallingConvention = CallingConvention.Winapi,
            EntryPoint = nameof(GetWindowLongW))]
        public static extern int GetWindowLongW(
            [In] IntPtr hWnd,
            [In] int nIndex);

        [DllImport("user32.dll",
            SetLastError = true,
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            CallingConvention = CallingConvention.Winapi,
            EntryPoint = nameof(SetWindowLongW))]
        public static extern int SetWindowLongW(
            [In] IntPtr hWnd,
            [In] int nIndex,
            [In] int dwNewLong);

        [DllImport("kernel32.dll", SetLastError = false, CharSet = CharSet.None, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetProductInfo(
             [In, MarshalAs(UnmanagedType.U4)] int dwOSMajorVersion,
             [In, MarshalAs(UnmanagedType.U4)] int dwOSMinorVersion,
             [In, MarshalAs(UnmanagedType.U4)] int dwSpMajorVersion,
             [In, MarshalAs(UnmanagedType.U4)] int dwSpMinorVersion,
             [Out, MarshalAs(UnmanagedType.U4)] out OSEdition pdwReturnedProductType);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetVersionExW(
            [In, Out] ref OSVERSIONINFOEXW osvi);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct OSVERSIONINFOEXW
        {
            [MarshalAs(UnmanagedType.U4)]
            public int dwOSVersionInfoSize;

            [MarshalAs(UnmanagedType.U4)]
            public int dwMajorVersion;

            [MarshalAs(UnmanagedType.U4)]
            public int dwMinorVersion;

            [MarshalAs(UnmanagedType.U4)]
            public int dwBuildNumber;

            [MarshalAs(UnmanagedType.U4)]
            public int dwPlatformId;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szCSDVersion;

            [MarshalAs(UnmanagedType.U2)]
            public short wServicePackMajor;

            [MarshalAs(UnmanagedType.U2)]
            public short wServicePackMinor;

            [MarshalAs(UnmanagedType.U2)]
            public SuiteMask wSuiteMask;

            [MarshalAs(UnmanagedType.I1)]
            public ProductType wProductType;

            [MarshalAs(UnmanagedType.I1)]
            public byte wReserved;
        }

        public enum ProductType : byte
        {
            VER_NT_DOMAIN_CONTROLLER = 0x0000002,
            VER_NT_SERVER = 0x0000003,
            VER_NT_WORKSTATION = 0x0000001,
        }

        [Flags]
        public enum SuiteMask : short
        {
            VER_SUITE_BACKOFFICE = 0x00000004,
            VER_SUITE_BLADE = 0x00000400,
            VER_SUITE_COMPUTE_SERVER = 0x00004000,
            VER_SUITE_DATACENTER = 0x00000080,
            VER_SUITE_ENTERPRISE = 0x00000002,
            VER_SUITE_EMBEDDEDNT = 0x00000040,
            VER_SUITE_PERSONAL = 0x00000200,
            VER_SUITE_SINGLEUSERTS = 0x00000100,
            VER_SUITE_SMALLBUSINESS = 0x00000001,
            VER_SUITE_SMALLBUSINESS_RESTRICTED = 0x00000020,
            VER_SUITE_STORAGE_SERVER = 0x00002000,
            VER_SUITE_TERMINAL = 0x00000010,
            VER_SUITE_WH_SERVER = unchecked((short)0x00008000u),
            VER_SUITE_MULTIUSERTS = unchecked((short)0x00020000),
        }

        public enum OSEdition : int
        {
            // https://docs.microsoft.com/ko-kr/windows/win32/api/sysinfoapi/nf-sysinfoapi-getproductinfo
            PRODUCT_BUSINESS = 0x00000006, // Business
            PRODUCT_BUSINESS_N = 0x00000010, // Business N
            PRODUCT_CLUSTER_SERVER = 0x00000012, // HPC Edition
            PRODUCT_CLUSTER_SERVER_V = 0x00000040, // Server Hyper Core V
            PRODUCT_CORE = 0x00000065, // Windows 10 Home
            PRODUCT_CORE_COUNTRYSPECIFIC = 0x00000063, // Windows 10 Home China
            PRODUCT_CORE_N = 0x00000062, // Windows 10 Home N
            PRODUCT_CORE_SINGLELANGUAGE = 0x00000064, // Windows 10 Home Single Language
            PRODUCT_DATACENTER_EVALUATION_SERVER = 0x00000050, // Server Datacenter (evaluation installation)
            PRODUCT_DATACENTER_A_SERVER_CORE = 0x00000091, // Server Datacenter, Semi-Annual Channel (core installation)
            PRODUCT_STANDARD_A_SERVER_CORE = 0x00000092, // Server Standard, Semi-Annual Channel (core installation)
            PRODUCT_DATACENTER_SERVER = 0x00000008, // Server Datacenter (full installation. For Server Core installations of Windows Server 2012 and later, use the method, Determining whether Server Core is running.)
            PRODUCT_DATACENTER_SERVER_CORE = 0x0000000C, // Server Datacenter (core installation, Windows Server 2008 R2 and earlier)
            PRODUCT_DATACENTER_SERVER_CORE_V = 0x00000027, // Server Datacenter without Hyper-V (core installation)
            PRODUCT_DATACENTER_SERVER_V = 0x00000025, // Server Datacenter without Hyper-V (full installation)
            PRODUCT_EDUCATION = 0x00000079, // Windows 10 Education
            PRODUCT_EDUCATION_N = 0x0000007A, // Windows 10 Education N
            PRODUCT_ENTERPRISE = 0x00000004, // Windows 10 Enterprise
            PRODUCT_ENTERPRISE_E = 0x00000046, // Windows 10 Enterprise E
            PRODUCT_ENTERPRISE_EVALUATION = 0x00000048, // Windows 10 Enterprise Evaluation
            PRODUCT_ENTERPRISE_N = 0x0000001B, // Windows 10 Enterprise N
            PRODUCT_ENTERPRISE_N_EVALUATION = 0x00000054, // Windows 10 Enterprise N Evaluation
            PRODUCT_ENTERPRISE_S = 0x0000007D, // Windows 10 Enterprise 2015 LTSB
            PRODUCT_ENTERPRISE_S_EVALUATION = 0x00000081, // Windows 10 Enterprise 2015 LTSB Evaluation
            PRODUCT_ENTERPRISE_S_N = 0x0000007E, // Windows 10 Enterprise 2015 LTSB N
            PRODUCT_ENTERPRISE_S_N_EVALUATION = 0x00000082, // Windows 10 Enterprise 2015 LTSB N Evaluation
            PRODUCT_ENTERPRISE_SERVER = 0x0000000A, // Server Enterprise (full installation)
            PRODUCT_ENTERPRISE_SERVER_CORE = 0x0000000E, // Server Enterprise (core installation)
            PRODUCT_ENTERPRISE_SERVER_CORE_V = 0x00000029, // Server Enterprise without Hyper-V (core installation)
            PRODUCT_ENTERPRISE_SERVER_IA64 = 0x0000000F, // Server Enterprise for Itanium-based Systems
            PRODUCT_ENTERPRISE_SERVER_V = 0x00000026, // Server Enterprise without Hyper-V (full installation)
            PRODUCT_ESSENTIALBUSINESS_SERVER_ADDL = 0x0000003C, // Windows Essential Server Solution Additional
            PRODUCT_ESSENTIALBUSINESS_SERVER_ADDLSVC = 0x0000003E, // Windows Essential Server Solution Additional SVC
            PRODUCT_ESSENTIALBUSINESS_SERVER_MGMT = 0x0000003B, // Windows Essential Server Solution Management
            PRODUCT_ESSENTIALBUSINESS_SERVER_MGMTSVC = 0x0000003D, // Windows Essential Server Solution Management SVC
            PRODUCT_HOME_BASIC = 0x00000002, // Home Basic
            PRODUCT_HOME_BASIC_E = 0x00000043, // Not supported
            PRODUCT_HOME_BASIC_N = 0x00000005, // Home Basic N
            PRODUCT_HOME_PREMIUM = 0x00000003, // Home Premium
            PRODUCT_HOME_PREMIUM_E = 0x00000044, // Not supported
            PRODUCT_HOME_PREMIUM_N = 0x0000001A, // Home Premium N
            PRODUCT_HOME_PREMIUM_SERVER = 0x00000022, // Windows Home Server 2011
            PRODUCT_HOME_SERVER = 0x00000013, // Windows Storage Server 2008 R2 Essentials
            PRODUCT_HYPERV = 0x0000002A, // Microsoft Hyper-V Server
            PRODUCT_IOTENTERPRISE = 0x000000BC, // Windows IoT Enterprise
            PRODUCT_IOTENTERPRISE_S = 0x000000BF, // Windows IoT Enterprise LTSC
            PRODUCT_IOTUAP = 0x0000007B, // Windows 10 IoT Core
            PRODUCT_IOTUAPCOMMERCIAL = 0x00000083, // Windows 10 IoT Core Commercial
            PRODUCT_MEDIUMBUSINESS_SERVER_MANAGEMENT = 0x0000001E, // Windows Essential Business Server Management Server
            PRODUCT_MEDIUMBUSINESS_SERVER_MESSAGING = 0x00000020, // Windows Essential Business Server Messaging Server
            PRODUCT_MEDIUMBUSINESS_SERVER_SECURITY = 0x0000001F, // Windows Essential Business Server Security Server
            PRODUCT_MOBILE_CORE = 0x00000068, // Windows 10 Mobile
            PRODUCT_MOBILE_ENTERPRISE = 0x00000085, // Windows 10 Mobile Enterprise
            PRODUCT_MULTIPOINT_PREMIUM_SERVER = 0x0000004D, // Windows MultiPoint Server Premium (full installation)
            PRODUCT_MULTIPOINT_STANDARD_SERVER = 0x0000004C, // Windows MultiPoint Server Standard (full installation)
            PRODUCT_PRO_WORKSTATION = 0x000000A1, // Windows 10 Pro for Workstations
            PRODUCT_PRO_WORKSTATION_N = 0x000000A2, // Windows 10 Pro for Workstations N
            PRODUCT_PROFESSIONAL = 0x00000030, // Windows 10 Pro
            PRODUCT_PROFESSIONAL_E = 0x00000045, // Not supported
            PRODUCT_PROFESSIONAL_N = 0x00000031, // Windows 10 Pro N
            PRODUCT_PROFESSIONAL_WMC = 0x00000067, // Professional with Media Center
            PRODUCT_SB_SOLUTION_SERVER = 0x00000032, // Windows Small Business Server 2011 Essentials
            PRODUCT_SB_SOLUTION_SERVER_EM = 0x00000036, // Server For SB Solutions EM
            PRODUCT_SERVER_FOR_SB_SOLUTIONS = 0x00000033, // Server For SB Solutions
            PRODUCT_SERVER_FOR_SB_SOLUTIONS_EM = 0x00000037, // Server For SB Solutions EM
            PRODUCT_SERVER_FOR_SMALLBUSINESS = 0x00000018, // Windows Server 2008 for Windows Essential Server Solutions
            PRODUCT_SERVER_FOR_SMALLBUSINESS_V = 0x00000023, // Windows Server 2008 without Hyper-V for Windows Essential Server Solutions
            PRODUCT_SERVER_FOUNDATION = 0x00000021, // Server Foundation
            PRODUCT_SMALLBUSINESS_SERVER = 0x00000009, // Windows Small Business Server
            PRODUCT_SMALLBUSINESS_SERVER_PREMIUM = 0x00000019, // Small Business Server Premium
            PRODUCT_SMALLBUSINESS_SERVER_PREMIUM_CORE = 0x0000003F, // Small Business Server Premium (core installation)
            PRODUCT_SOLUTION_EMBEDDEDSERVER = 0x00000038, // Windows MultiPoint Server
            PRODUCT_STANDARD_EVALUATION_SERVER = 0x0000004F, // Server Standard (evaluation installation)
            PRODUCT_STANDARD_SERVER = 0x00000007, // Server Standard (full installation. For Server Core installations of Windows Server 2012 and later, use the method, Determining whether Server Core is running.)
            PRODUCT_STANDARD_SERVER_CORE = 0x0000000D, // Server Standard (core installation, Windows Server 2008 R2 and earlier)
            PRODUCT_STANDARD_SERVER_CORE_V = 0x00000028, // Server Standard without Hyper-V (core installation)
            PRODUCT_STANDARD_SERVER_V = 0x00000024, // Server Standard without Hyper-V
            PRODUCT_STANDARD_SERVER_SOLUTIONS = 0x00000034, // Server Solutions Premium
            PRODUCT_STANDARD_SERVER_SOLUTIONS_CORE = 0x00000035, // Server Solutions Premium (core installation)
            PRODUCT_STARTER = 0x0000000B, // Starter
            PRODUCT_STARTER_E = 0x00000042, // Not supported
            PRODUCT_STARTER_N = 0x0000002F, // Starter N
            PRODUCT_STORAGE_ENTERPRISE_SERVER = 0x00000017, // Storage Server Enterprise
            PRODUCT_STORAGE_ENTERPRISE_SERVER_CORE = 0x0000002E, // Storage Server Enterprise (core installation)
            PRODUCT_STORAGE_EXPRESS_SERVER = 0x00000014, // Storage Server Express
            PRODUCT_STORAGE_EXPRESS_SERVER_CORE = 0x0000002B, // Storage Server Express (core installation)
            PRODUCT_STORAGE_STANDARD_EVALUATION_SERVER = 0x00000060, // Storage Server Standard (evaluation installation)
            PRODUCT_STORAGE_STANDARD_SERVER = 0x00000015, // Storage Server Standard
            PRODUCT_STORAGE_STANDARD_SERVER_CORE = 0x0000002C, // Storage Server Standard (core installation)
            PRODUCT_STORAGE_WORKGROUP_EVALUATION_SERVER = 0x0000005F, // Storage Server Workgroup (evaluation installation)
            PRODUCT_STORAGE_WORKGROUP_SERVER = 0x00000016, // Storage Server Workgroup
            PRODUCT_STORAGE_WORKGROUP_SERVER_CORE = 0x0000002D, // Storage Server Workgroup (core installation)
            PRODUCT_ULTIMATE = 0x00000001, // Ultimate
            PRODUCT_ULTIMATE_E = 0x00000047, // Not supported
            PRODUCT_ULTIMATE_N = 0x0000001C, // Ultimate N
            PRODUCT_UNDEFINED = 0x00000000, // An unknown product
            PRODUCT_WEB_SERVER = 0x00000011, // Web Server (full installation)
            PRODUCT_WEB_SERVER_CORE = 0x0000001D, // Web Server (core installation)
        }

        public static readonly Guid DownloadFolderGuid = new Guid("{374DE290-123F-4565-9164-39C4925E467B}");

        public static string GetKnownFolderPath(Guid knownFolderGuid, KnownFolderFlags flags = KnownFolderFlags.DontVerify, bool defaultUser = false)
        {
            var result = SHGetKnownFolderPath(knownFolderGuid, (int)flags, new IntPtr(defaultUser ? -1 : 0), out var outPath);

            if (result >= 0)
            {
                var path = Marshal.PtrToStringUni(outPath);
                Marshal.FreeCoTaskMem(outPath);
                return path;
            }
            else
            {
                throw new ExternalException("Unable to retrieve the known folder path. It may not be available on this system.", result);
            }
        }

        [DllImport("shell32.dll",
            SetLastError = false,
            CharSet = CharSet.None,
            ExactSpelling = true,
            EntryPoint = nameof(SHGetKnownFolderPath),
            CallingConvention = CallingConvention.StdCall)]
        private static extern int SHGetKnownFolderPath(
            [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
            [MarshalAs(UnmanagedType.U4)] int dwFlags,
            IntPtr hToken, out IntPtr ppszPath);

        [Flags]
        public enum KnownFolderFlags : uint
        {
            SimpleIDList = 0x00000100,
            NotParentRelative = 0x00000200,
            DefaultPath = 0x00000400,
            Init = 0x00000800,
            NoAlias = 0x00001000,
            DontUnexpand = 0x00002000,
            DontVerify = 0x00004000,
            Create = 0x00008000,
            NoAppcontainerRedirection = 0x00010000,
            AliasOnly = 0x80000000
        }

        public const int SetDesktopWallpaper = 0x0014;
        public const int UpdateIniFile = 0x01;
        public const int SendWinIniChange = 0x02;

        [DllImport("user32.dll",
            SetLastError = true,
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            EntryPoint = nameof(SystemParametersInfoW),
            CallingConvention = CallingConvention.Winapi)]
        public static extern int SystemParametersInfoW(
            [MarshalAs(UnmanagedType.U4)] int uAction,
            [MarshalAs(UnmanagedType.U4)] int uParam,
            string lpvParam,
            [MarshalAs(UnmanagedType.U4)] int fuWinIni);

        [DllImport("user32.dll",
            SetLastError = false,
            CharSet = CharSet.Ansi,
            ExactSpelling = true,
            EntryPoint = nameof(UpdatePerUserSystemParameters),
            CallingConvention = CallingConvention.StdCall)]
        public static extern void UpdatePerUserSystemParameters(
            IntPtr hWnd,
            IntPtr hInstance,
            string lpszCmdLine,
            int nCmdShow);
    }
}
