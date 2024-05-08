using System;
using System.IO;
using System.Linq;
using TableCloth.Components.Implementations.Internals;

namespace TableCloth.Components.Implementations;

public sealed class SystemProperties : ISystemProperties
{
    public bool? IsSystemDiskAHardDrive()
    {
        var systemDisk = Win32DiskDrive.GetPhysicalDisks().FirstOrDefault(x => x.IsSystemDisk);

        if (systemDisk == default)
            return default;

        // 0: Unspecified, 3: HDD, 4: SSD, 5: SCM
        return systemDisk.MediaType == 3;
    }

    public bool? IsSystemPartitionBitLockerEnabled()
    {
        var shellType = Type.GetTypeFromProgID("Shell.Application").EnsureNotNull("Cannot obtain Shell.Application type.");
        object oInstance = Activator.CreateInstance(shellType).EnsureNotNull("Cannot create instance of Shell.Application.");
        dynamic shell = oInstance;

        // https://www.reddit.com/r/PowerShell/comments/jl12ux/comment/gamgwhj/
        var systemDrivePath = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        var protectionStatus = ((int?)shell?.NameSpace(systemDrivePath)?.Self?.ExtendedProperty("System.Volume.BitLockerProtection")) ?? 0;

        if (protectionStatus == default)
            return default;

        // 0: Unprotectable, 1: Protected, 2: Not Protected, 3: Protection In Progress
        return (protectionStatus == 1);
    }
}
