using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;

namespace Hostess
{
    // https://stackoverflow.com/questions/15771998/how-to-give-a-user-permission-to-start-and-stop-a-particular-service-using-c-sha
    // https://docs.microsoft.com/en-us/windows/win32/services/service-security-and-access-rights

    internal static class ProtectTermService
    {
        internal const int SERVICE_CHANGE_CONFIG = 0x0002;
        internal const int SERVICE_PAUSE_CONTINUE = 0x0040;
        internal const int SERVICE_STOP = 0x0020;

        internal const int ERROR_INSUFFICIENT_BUFFER = 0x7a;
        internal const int SC_STATUS_PROCESS_INFO = 0;

        public enum SE_OBJECT_TYPE : int
        {
            SE_UNKNOWN_OBJECT_TYPE,
            SE_FILE_OBJECT,
            SE_SERVICE,
            SE_PRINTER,
            SE_REGISTRY_KEY,
            SE_LMSHARE,
            SE_KERNEL_OBJECT,
            SE_WINDOW_OBJECT,
            SE_DS_OBJECT,
            SE_DS_OBJECT_ALL,
            SE_PROVIDER_DEFINED_OBJECT,
            SE_WMIGUID_OBJECT,
            SE_REGISTRY_WOW64_32KEY
        }

        [Flags]
        public enum SECURITY_INFORMATION : int
        {
            OWNER_SECURITY_INFORMATION = 1,
            GROUP_SECURITY_INFORMATION = 2,
            DACL_SECURITY_INFORMATION = 4,
            SACL_SECURITY_INFORMATION = 8,
        }

        [StructLayout(LayoutKind.Sequential)]
        public sealed class SERVICE_STATUS_PROCESS
        {
            [MarshalAs(UnmanagedType.U4)]
            public int dwServiceType;
            [MarshalAs(UnmanagedType.U4)]
            public int dwCurrentState;
            [MarshalAs(UnmanagedType.U4)]
            public int dwControlsAccepted;
            [MarshalAs(UnmanagedType.U4)]
            public int dwWin32ExitCode;
            [MarshalAs(UnmanagedType.U4)]
            public int dwServiceSpecificExitCode;
            [MarshalAs(UnmanagedType.U4)]
            public int dwCheckPoint;
            [MarshalAs(UnmanagedType.U4)]
            public int dwWaitHint;
            [MarshalAs(UnmanagedType.U4)]
            public int dwProcessId;
            [MarshalAs(UnmanagedType.U4)]
            public int dwServiceFlags;
        }

        [Flags]
        public enum ProcessAccessRights
        {
            /// <summary>
            /// PROCESS_CREATE_PROCESS
            /// </summary>
            PROCESS_CREATE_PROCESS = 0x0080,

            /// <summary>
            /// PROCESS_CREATE_THREAD
            /// </summary>
            PROCESS_CREATE_THREAD = 0x0002,

            /// <summary>
            /// PROCESS_DUP_HANDLE
            /// </summary>
            PROCESS_DUP_HANDLE = 0x0040,

            /// <summary>
            /// PROCESS_QUERY_INFORMATION
            /// </summary>
            PROCESS_QUERY_INFORMATION = 0x0400,

            /// <summary>
            /// PROCESS_QUERY_LIMITED_INFORMATION
            /// </summary>
            PROCESS_QUERY_LIMITED_INFORMATION = 0x1000,

            /// <summary>
            /// PROCESS_SET_INFORMATION
            /// </summary>
            PROCESS_SET_INFORMATION = 0x0200,

            /// <summary>
            /// PROCESS_SET_QUOTA
            /// </summary>
            PROCESS_SET_QUOTA = 0x0100,

            /// <summary>
            /// PROCESS_SUSPEND_RESUME
            /// </summary>
            PROCESS_SUSPEND_RESUME = 0x0800,

            /// <summary>
            /// PROCESS_TERMINATE
            /// </summary>
            PROCESS_TERMINATE = 0x0001,

            /// <summary>
            /// PROCESS_VM_OPERATION
            /// </summary>
            PROCESS_VM_OPERATION = 0x0008,

            /// <summary>
            /// PROCESS_VM_READ
            /// </summary>
            PROCESS_VM_READ = 0x0010,

            /// <summary>
            /// PROCESS_VM_WRITE
            /// </summary>
            PROCESS_VM_WRITE = 0x0020,

            /// <summary>
            /// DELETE
            /// </summary>
            DELETE = 0x00010000,

            /// <summary>
            /// READ_CONTROL
            /// </summary>
            READ_CONTROL = 0x00020000,

            /// <summary>
            /// SYNCHRONIZE
            /// </summary>
            SYNCHRONIZE = 0x00100000,

            /// <summary>
            /// WRITE_DAC
            /// </summary>
            WRITE_DAC = 0x00040000,

            /// <summary>
            /// WRITE_OWNER
            /// </summary>
            WRITE_OWNER = 0x00080000,

            /// <summary>
            /// STANDARD_RIGHTS_REQUIRED
            /// </summary>
            STANDARD_RIGHTS_REQUIRED = 0x000f0000,

            /// <summary>
            /// PROCESS_ALL_ACCESS
            /// </summary>
            PROCESS_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED | SYNCHRONIZE | 0xFFF)
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SECURITY_DESCRIPTOR
        {
            public byte revision;
            public byte size;
            public short control;
            public IntPtr owner;
            public IntPtr group;
            public IntPtr sacl;
            public IntPtr dacl;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool QueryServiceStatusEx(
            SafeHandle hService,
            int infoLevel,
            IntPtr lpBuffer,
            [MarshalAs(UnmanagedType.U4)] int cbBufSize,
            [MarshalAs(UnmanagedType.U4)] out int pcbBytesNeeded);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool QueryServiceObjectSecurity(IntPtr serviceHandle,
            SecurityInfos secInfo,
            ref SECURITY_DESCRIPTOR lpSecDesrBuf,
            [MarshalAs(UnmanagedType.U4)] int bufSize,
            [MarshalAs(UnmanagedType.U4)] out int bufSizeNeeded);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool QueryServiceObjectSecurity(SafeHandle serviceHandle,
            SecurityInfos secInfo,
            byte[] lpSecDesrBuf,
            [MarshalAs(UnmanagedType.U4)] int bufSize,
            [MarshalAs(UnmanagedType.U4)] out int bufSizeNeeded);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool SetServiceObjectSecurity(SafeHandle serviceHandle,
            SecurityInfos secInfos,
            byte[] lpSecDesrBuf);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool GetKernelObjectSecurity(
            IntPtr handle,
            int securityInformation,
            [Out] byte[] securityDescriptor,
            [MarshalAs(UnmanagedType.U4)] int length,
            [MarshalAs(UnmanagedType.U4)] out int lengthNeeded);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern uint GetSecurityInfo(
            IntPtr handle,
            SE_OBJECT_TYPE ObjectType,
            SECURITY_INFORMATION SecurityInfo,
            out IntPtr pSidOwner,
            out IntPtr pSidGroup,
            out IntPtr pDacl,
            out IntPtr pSacl,
            out IntPtr pSecurityDescriptor);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool SetKernelObjectSecurity(
            IntPtr handle,
            int securityInformation,
            [In] byte[] securityDescriptor);

        public static void PreventServiceProcessTermination(string service)
        {
            var sc = new ServiceController(service, ".");

            IntPtr processHandle = Process.GetProcessById(GetServiceProcessId(sc)).Handle;

            RawSecurityDescriptor descriptor = GetProcessSecurityDescriptor(processHandle);

            for (int i = descriptor.DiscretionaryAcl.Count - 1; i > 0; i--)
                descriptor.DiscretionaryAcl.RemoveAce(i);

            descriptor.DiscretionaryAcl.InsertAce(0, new CommonAce(
                AceFlags.None,
                AceQualifier.AccessDenied,
                (int)ProcessAccessRights.PROCESS_ALL_ACCESS,
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                false,
                null));

            SetProcessSecurityDescriptor(processHandle, descriptor);
        }

        private static RawSecurityDescriptor GetProcessSecurityDescriptor(IntPtr processHandle)
        {
            byte[] byteArray = new byte[0];
            int bufferSizeNeeded;

            GetKernelObjectSecurity(
                processHandle,
                (int)SECURITY_INFORMATION.DACL_SECURITY_INFORMATION,
                byteArray,
                0,
                out bufferSizeNeeded);

            if (bufferSizeNeeded < 0 || bufferSizeNeeded > short.MaxValue)
                throw new Win32Exception();

            if (!GetKernelObjectSecurity(processHandle, (int)SECURITY_INFORMATION.DACL_SECURITY_INFORMATION, byteArray = new byte[bufferSizeNeeded], bufferSizeNeeded, out bufferSizeNeeded))
                throw new Win32Exception();

            return new RawSecurityDescriptor(byteArray, 0);
        }

        private static void SetProcessSecurityDescriptor(IntPtr processHandle, RawSecurityDescriptor descriptor)
        {
            byte[] byteArray = new byte[descriptor.BinaryLength];
            descriptor.GetBinaryForm(byteArray, 0);

            if (!SetKernelObjectSecurity(processHandle, (int)SECURITY_INFORMATION.DACL_SECURITY_INFORMATION, byteArray))
                throw new Win32Exception();
        }

        public static int GetServiceProcessId(ServiceController sc)
        {
            if (sc == null)
                throw new ArgumentNullException(nameof(sc));

            IntPtr zero = IntPtr.Zero;

            try
            {
                int dwBytesNeeded;
                // Call once to figure the size of the output buffer.
                QueryServiceStatusEx(sc.ServiceHandle, SC_STATUS_PROCESS_INFO, zero, 0, out dwBytesNeeded);
                if (Marshal.GetLastWin32Error() == ERROR_INSUFFICIENT_BUFFER)
                {
                    // Allocate required buffer and call again.
                    zero = Marshal.AllocHGlobal(dwBytesNeeded);

                    if (QueryServiceStatusEx(sc.ServiceHandle, SC_STATUS_PROCESS_INFO, zero, dwBytesNeeded, out dwBytesNeeded))
                    {
                        var ssp = new SERVICE_STATUS_PROCESS();
                        Marshal.PtrToStructure(zero, ssp);
                        return (int)ssp.dwProcessId;
                    }
                }
            }
            finally
            {
                if (zero != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(zero);
                }
            }
            return -1;
        }

        public static void PreventServiceStop(string service, string username)
        {
            var sc = new ServiceController(service, ".");
            var status = sc.Status;
            var psd = new byte[0];
            var bufSizeNeeded = 0;
            var ok = QueryServiceObjectSecurity(sc.ServiceHandle, SecurityInfos.DiscretionaryAcl, psd, 0, out bufSizeNeeded);

            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                if (err == 122 || err == 0)
                {
                    // ERROR_INSUFFICIENT_BUFFER
                    // expected; now we know bufsize
                    psd = new byte[bufSizeNeeded];
                    ok = QueryServiceObjectSecurity(sc.ServiceHandle, SecurityInfos.DiscretionaryAcl, psd, bufSizeNeeded, out bufSizeNeeded);
                }
                else
                {
                    throw new ApplicationException("error calling QueryServiceObjectSecurity() to get DACL for " + service + ": error code=" + err);
                }
            }

            if (!ok)
                throw new ApplicationException("error calling QueryServiceObjectSecurity(2) to get DACL for " + service + ": error code=" + Marshal.GetLastWin32Error());

            // get security descriptor via raw into DACL form so ACE
            // ordering checks are done for us.
            var rsd = new RawSecurityDescriptor(psd, 0);
            var racl = rsd.DiscretionaryAcl;
            var dacl = new DiscretionaryAcl(false, false, racl);

            // Add start/stop/read access
            var acct = new NTAccount(username);
            var sid = (SecurityIdentifier)acct.Translate(typeof(SecurityIdentifier));
            dacl.AddAccess(AccessControlType.Deny, sid,
                (SERVICE_CHANGE_CONFIG | SERVICE_STOP | SERVICE_PAUSE_CONTINUE),
                InheritanceFlags.None, PropagationFlags.None);

            // convert discretionary ACL back to raw form; looks like via byte[] is only way
            var rawdacl = new byte[dacl.BinaryLength];
            dacl.GetBinaryForm(rawdacl, 0);
            rsd.DiscretionaryAcl = new RawAcl(rawdacl, 0);

            // set raw security descriptor on service again
            var rawsd = new byte[rsd.BinaryLength];
            rsd.GetBinaryForm(rawsd, 0);
            ok = SetServiceObjectSecurity(sc.ServiceHandle, SecurityInfos.DiscretionaryAcl, rawsd);

            if (!ok)
                throw new ApplicationException("error calling SetServiceObjectSecurity(); error code=" + Marshal.GetLastWin32Error());
        }
    }
}
