using System;
using System.Management;

namespace TableCloth.Components.Implementations.Internals;

public sealed class Win32DiskPartition
{
    public static void QueryPartitions(Win32DiskDrive drive, TimeSpan? timeout = default)
    {
        using var assocPart = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{drive.DeviceId}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition");
        assocPart.Options.Timeout = timeout ?? System.Management.EnumerationOptions.InfiniteTimeout;

        foreach (var driveToPartition in assocPart.Get())
        {
            var logDiskQuery = $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{driveToPartition.Properties["DeviceID"].Value.ToString()}'}} WHERE AssocClass = Win32_LogicalDiskToPartition";

            using var logDisk = new ManagementObjectSearcher(logDiskQuery);
            logDisk.Options.Timeout = timeout ?? System.Management.EnumerationOptions.InfiniteTimeout;

            foreach (var diskToPartition in logDisk.Get())
            {
                var partition = new Win32DiskPartition();
                partition.Compressed = (bool)diskToPartition.Properties["Compressed"].Value;
                partition.DeviceId = (string)diskToPartition.Properties["DeviceID"].Value;
                partition.Caption = (string)diskToPartition.Properties["Caption"].Value;
                partition.Description = (string)diskToPartition.Properties["Description"].Value;
                partition.DriveType = (int)(uint)diskToPartition.Properties["DriveType"].Value;
                partition.FileSystem = (string)diskToPartition.Properties["FileSystem"].Value;
                partition.FreeSpace = (long)(ulong)diskToPartition.Properties["FreeSpace"].Value;
                partition.MediaType = (int)(uint)diskToPartition.Properties["MediaType"].Value;
                partition.Name = (string)diskToPartition.Properties["Name"].Value;
                partition.VolumeName = (string)diskToPartition.Properties["VolumeName"].Value;
                partition.VolumeSerialNumber = (string)diskToPartition.Properties["VolumeSerialNumber"].Value;
                drive.Partitions.Add(partition);
            }
        }
    }

    public bool Compressed { get; private set; } = false;
    public string DeviceId { get; private set; } = string.Empty;
    public string Caption { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int DriveType { get; private set; } = default;
    public string FileSystem { get; private set; } = string.Empty;
    public long FreeSpace { get; private set; } = default;
    public int MediaType { get; private set; } = default;
    public string Name { get; private set; } = string.Empty;
    public long Size { get; private set; } = default;
    public string VolumeName { get; private set; } = string.Empty;
    public string VolumeSerialNumber { get; private set; } = string.Empty;
}
