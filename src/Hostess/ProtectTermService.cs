using System;
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

        public static void DenyServiceStop(string service, string username)
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
