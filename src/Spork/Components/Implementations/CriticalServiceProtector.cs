using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using TableCloth;

namespace Spork.Components.Implementations
{
    // https://stackoverflow.com/questions/15771998/how-to-give-a-user-permission-to-start-and-stop-a-particular-service-using-c-sha
    // https://docs.microsoft.com/en-us/windows/win32/services/service-security-and-access-rights

    public sealed class CriticalServiceProtector : ICriticalServiceProtector
    {
        public void PreventServiceProcessTermination(string service)
        {
            var sc = new ServiceController(service, ".");

            IntPtr processHandle = Process.GetProcessById(GetServiceProcessId(sc)).Handle;

            RawSecurityDescriptor descriptor = GetProcessSecurityDescriptor(processHandle);

            for (int i = descriptor.DiscretionaryAcl.Count - 1; i > 0; i--)
                descriptor.DiscretionaryAcl.RemoveAce(i);

            descriptor.DiscretionaryAcl.InsertAce(0, new CommonAce(
                AceFlags.None,
                AceQualifier.AccessDenied,
                (int)NativeMethods.ProcessAccessRights.PROCESS_ALL_ACCESS,
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                false,
                null));

            SetProcessSecurityDescriptor(processHandle, descriptor);
        }

        private RawSecurityDescriptor GetProcessSecurityDescriptor(IntPtr processHandle)
        {
            byte[] byteArray = new byte[0];
            int bufferSizeNeeded;

            NativeMethods.GetKernelObjectSecurity(
                processHandle,
                (int)NativeMethods.SECURITY_INFORMATION.DACL_SECURITY_INFORMATION,
                byteArray,
                0,
                out bufferSizeNeeded);

            if (bufferSizeNeeded < 0 || bufferSizeNeeded > short.MaxValue)
                throw new Win32Exception();

            if (!NativeMethods.GetKernelObjectSecurity(processHandle, (int)NativeMethods.SECURITY_INFORMATION.DACL_SECURITY_INFORMATION, byteArray = new byte[bufferSizeNeeded], bufferSizeNeeded, out bufferSizeNeeded))
                throw new Win32Exception();

            return new RawSecurityDescriptor(byteArray, 0);
        }

        private void SetProcessSecurityDescriptor(IntPtr processHandle, RawSecurityDescriptor descriptor)
        {
            byte[] byteArray = new byte[descriptor.BinaryLength];
            descriptor.GetBinaryForm(byteArray, 0);

            if (!NativeMethods.SetKernelObjectSecurity(processHandle, (int)NativeMethods.SECURITY_INFORMATION.DACL_SECURITY_INFORMATION, byteArray))
                throw new Win32Exception();
        }

        public int GetServiceProcessId(ServiceController sc)
        {
            sc = sc.EnsureArgumentNotNull("Service controller cannot be null reference.", nameof(sc));

            var zero = IntPtr.Zero;

            try
            {
                int dwBytesNeeded;
                // Call once to figure the size of the output buffer.
                NativeMethods.QueryServiceStatusEx(sc.ServiceHandle, NativeMethods.SC_STATUS_PROCESS_INFO, zero, 0, out dwBytesNeeded);
                if (Marshal.GetLastWin32Error() == NativeMethods.ERROR_INSUFFICIENT_BUFFER)
                {
                    // Allocate required buffer and call again.
                    zero = Marshal.AllocHGlobal(dwBytesNeeded);

                    if (NativeMethods.QueryServiceStatusEx(sc.ServiceHandle, NativeMethods.SC_STATUS_PROCESS_INFO, zero, dwBytesNeeded, out dwBytesNeeded))
                    {
                        var ssp = new NativeMethods.SERVICE_STATUS_PROCESS();
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

        public void PreventServiceStop(string service, string username)
        {
            var sc = new ServiceController(service, ".");
            var status = sc.Status;
            var psd = new byte[0];
            var bufSizeNeeded = 0;
            var ok = NativeMethods.QueryServiceObjectSecurity(sc.ServiceHandle, SecurityInfos.DiscretionaryAcl, psd, 0, out bufSizeNeeded);

            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                if (err == 122 || err == 0)
                {
                    // ERROR_INSUFFICIENT_BUFFER
                    // expected; now we know bufsize
                    psd = new byte[bufSizeNeeded];
                    ok = NativeMethods.QueryServiceObjectSecurity(sc.ServiceHandle, SecurityInfos.DiscretionaryAcl, psd, bufSizeNeeded, out bufSizeNeeded);
                }
                else
                {
                    TableClothAppException.Throw("error calling QueryServiceObjectSecurity() to get DACL for " + service + ": error code=" + err);
                }
            }

            if (!ok)
                TableClothAppException.Throw("error calling QueryServiceObjectSecurity(2) to get DACL for " + service + ": error code=" + Marshal.GetLastWin32Error());

            // get security descriptor via raw into DACL form so ACE
            // ordering checks are done for us.
            var rsd = new RawSecurityDescriptor(psd, 0);
            var racl = rsd.DiscretionaryAcl;
            var dacl = new DiscretionaryAcl(false, false, racl);

            // Add start/stop/read access
            var acct = new NTAccount(username);
            var sid = (SecurityIdentifier)acct.Translate(typeof(SecurityIdentifier));
            dacl.AddAccess(AccessControlType.Deny, sid,
                (NativeMethods.SERVICE_CHANGE_CONFIG | NativeMethods.SERVICE_STOP | NativeMethods.SERVICE_PAUSE_CONTINUE),
                InheritanceFlags.None, PropagationFlags.None);

            // convert discretionary ACL back to raw form; looks like via byte[] is only way
            var rawdacl = new byte[dacl.BinaryLength];
            dacl.GetBinaryForm(rawdacl, 0);
            rsd.DiscretionaryAcl = new RawAcl(rawdacl, 0);

            // set raw security descriptor on service again
            var rawsd = new byte[rsd.BinaryLength];
            rsd.GetBinaryForm(rawsd, 0);
            ok = NativeMethods.SetServiceObjectSecurity(sc.ServiceHandle, SecurityInfos.DiscretionaryAcl, rawsd);

            if (!ok)
                TableClothAppException.Throw("error calling SetServiceObjectSecurity(); error code=" + Marshal.GetLastWin32Error());
        }
    }
}
