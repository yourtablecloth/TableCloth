using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace TableCloth.AppContainerPoc;

internal static unsafe partial class Win32
{
    internal const uint DaclInformation = 4, LabelInformation = 16;
    internal const uint SecurityInformation = DaclInformation | LabelInformation;
    internal const uint ReadExecute = 0x1200A9, Modify = 0x1301BF;

    internal static void Check(bool success)
    {
        if (!success) throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    internal static void CheckError(uint error)
    {
        if (error != 0) throw new Win32Exception((int)error);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SecurityAttributes
    {
        internal uint Length;
        internal nint Descriptor;
        internal int Inherit;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct StartupInfo
    {
        internal uint Size;
        internal nint Reserved, Desktop, Title;
        internal uint X, Y, Width, Height, XChars, YChars, Fill, Flags;
        internal ushort ShowWindow, ReservedSize;
        internal nint ReservedBytes, Input, Output, Error;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct StartupInfoEx
    {
        internal StartupInfo Startup;
        internal nint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ProcessInformation
    {
        internal nint Process, Thread;
        internal uint ProcessId, ThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SidAndAttributes
    {
        internal nint Sid;
        internal uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SecurityCapabilities
    {
        internal nint AppContainerSid, Capabilities;
        internal uint CapabilityCount, Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BasicLimits
    {
        internal long ProcessTime, JobTime;
        internal uint Flags;
        internal nuint MinimumWorkingSet, MaximumWorkingSet;
        internal uint ActiveProcesses;
        internal nuint Affinity;
        internal uint Priority, SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct IoCounters
    {
        internal ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ExtendedLimits
    {
        internal BasicLimits Basic;
        internal IoCounters Io;
        internal nuint ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Trustee
    {
        internal nint MultipleTrustee;
        internal uint MultipleTrusteeOperation, Form, Type;
        internal nint Name;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ExplicitAccess
    {
        internal uint Permissions, Mode, Inheritance;
        internal Trustee Trustee;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FileTime
    {
        internal uint Low, High;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FileInformation
    {
        internal uint Attributes;
        internal FileTime Created, Accessed, Written;
        internal uint VolumeSerial, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }

    [LibraryImport("userenv.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int CreateAppContainerProfile(string name, string displayName, string description,
        SidAndAttributes* capabilities, uint count, out nint sid);

    [LibraryImport("userenv.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int DeleteAppContainerProfile(string name);

    [LibraryImport("advapi32.dll")]
    internal static partial nint FreeSid(nint sid);

    [LibraryImport("kernel32.dll")]
    internal static partial nint LocalFree(nint memory);

    [LibraryImport("advapi32.dll", EntryPoint = "ConvertStringSidToSidW", SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ConvertStringSidToSid(string value, out nint sid);

    [LibraryImport("advapi32.dll", EntryPoint = "GetNamedSecurityInfoW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint GetNamedSecurityInfo(string path, uint type, uint information,
        out nint owner, out nint group, out nint dacl, out nint sacl, out nint descriptor);

    [LibraryImport("advapi32.dll", EntryPoint = "SetNamedSecurityInfoW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint SetNamedSecurityInfo(string path, uint type, uint information,
        nint owner, nint group, nint dacl, nint sacl);

    [LibraryImport("advapi32.dll", EntryPoint = "ConvertSecurityDescriptorToStringSecurityDescriptorW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DescriptorToString(nint descriptor, uint revision, uint information,
        out nint value, out uint length);

    [LibraryImport("advapi32.dll", EntryPoint = "ConvertStringSecurityDescriptorToSecurityDescriptorW", SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool StringToDescriptor(string value, uint revision, out nint descriptor, out uint length);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetSecurityDescriptorDacl(nint descriptor, out int present, out nint dacl, out int defaulted);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetSecurityDescriptorSacl(nint descriptor, out int present, out nint sacl, out int defaulted);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetSecurityDescriptorControl(nint descriptor, out ushort control, out uint revision);

    [LibraryImport("advapi32.dll", EntryPoint = "SetEntriesInAclW")]
    internal static partial uint SetEntriesInAcl(uint count, ref ExplicitAccess entry, nint oldAcl, out nint newAcl);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CreatePipe(out SafeFileHandle read, out SafeFileHandle write,
        ref SecurityAttributes attributes, uint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetHandleInformation(SafeFileHandle handle, uint mask, uint flags);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool InitializeProcThreadAttributeList(nint list, int count, uint flags, ref nuint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UpdateProcThreadAttribute(nint list, uint flags, nuint attribute,
        void* value, nuint size, nint previous, nint returnedSize);

    [LibraryImport("kernel32.dll")]
    internal static partial void DeleteProcThreadAttributeList(nint list);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateProcessW", SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CreateProcess(string application, char* commandLine, nint processSecurity,
        nint threadSecurity, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, char* environment,
        string directory, ref StartupInfoEx startup, out ProcessInformation process);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true)]
    internal static partial SafeFileHandle CreateJobObject(nint attributes, nint name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetInformationJobObject(SafeFileHandle job, int infoClass, void* info, uint length);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AssignProcessToJobObject(SafeFileHandle job, SafeFileHandle process);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool TerminateJobObject(SafeFileHandle job, uint code);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool TerminateProcess(SafeFileHandle process, uint code);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial uint ResumeThread(SafeFileHandle thread);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial uint WaitForSingleObject(SafeFileHandle handle, uint milliseconds);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetExitCodeProcess(SafeFileHandle process, out uint code);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool OpenProcessToken(SafeFileHandle process, uint access, out SafeFileHandle token);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetTokenInformation(SafeFileHandle token, int informationClass,
        out int value, uint length, out uint returned);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    internal static partial SafeFileHandle OpenMetadata(string path, uint access, uint share, nint security,
        uint creation, uint flags, nint template);
}
