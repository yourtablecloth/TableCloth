using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace TableCloth.Components.Implementations.Internals;

// https://www.sysnet.pe.kr/2/0/13228
// https://learn.microsoft.com/ko-kr/windows-hardware/drivers/storage/msft-physicaldisk
public sealed class Win32DiskDrive
{
    public static IReadOnlyList<Win32DiskDrive> GetPhysicalDisks(TimeSpan? timeout = default)
    {
        var systemDrive = GetSystemDrive(timeout);
        var drives = new List<Win32DiskDrive>();

        using var searcher = new ManagementObjectSearcher(@"\\.\Root\CIMV2", "SELECT * FROM Win32_DiskDrive");
        searcher.Options.Timeout = timeout ?? System.Management.EnumerationOptions.InfiniteTimeout;

        foreach (var queryObj in searcher.Get())
        {
            var item = new Win32DiskDrive();

            item.DeviceId = (string)queryObj.Properties["DeviceID"].Value;
            item.Caption = (string)queryObj.Properties["Caption"].Value;
            item.Description = (string)queryObj.Properties["Description"].Value;
            item.Index = (int)(uint)queryObj.Properties["Index"].Value;

            item.SerialNumber = ((string)queryObj.Properties["SerialNumber"].Value).Trim();
            item.PlugNPlayDeviceId = (string)queryObj.Properties["PNPDeviceID"].Value;

            QueryStorageInfo(item, timeout);
            Win32DiskPartition.QueryPartitions(item, timeout);
            item.IsSystemDisk = item.Partitions.Any(x => x.DeviceId == systemDrive);
            drives.Add(item);
        }

        return drives;
    }

    private static void QueryStorageInfo(Win32DiskDrive disk, TimeSpan? timeout = default)
    {
        using var searcher = new ManagementObjectSearcher(
            @"\\.\Root\Microsoft\Windows\Storage",
            @$"SELECT BusType, MediaType FROM MSFT_PhysicalDisk WHERE SerialNumber = '{disk.SerialNumber}'");
        searcher.Options.Timeout = timeout ?? System.Management.EnumerationOptions.InfiniteTimeout;

        foreach (var queryObj in searcher.Get())
        {
            disk.BusType = (short)(ushort)queryObj.Properties["BusType"].Value;
            disk.MediaType = (short)(ushort)queryObj.Properties["MediaType"].Value;
        }
    }

    private static string? GetSystemDrive(TimeSpan? timeout = default)
    {
        using var searcher = new ManagementObjectSearcher(
            @"\\.\Root\CIMV2",
            "SELECT SystemDrive FROM Win32_OperatingSystem");
        searcher.Options.Timeout = timeout ?? System.Management.EnumerationOptions.InfiniteTimeout;

        return searcher.Get().Cast<ManagementBaseObject>().Select(x => x.Properties.Cast<PropertyData>().Select(x => x.Value).FirstOrDefault()).FirstOrDefault() as string;
    }

    public string DeviceId { get; private set; } = string.Empty;
    public string Caption { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public int Index { get; private set; } = default;
    public string SerialNumber { get; private set; } = string.Empty;
    public string PlugNPlayDeviceId { get; private set; } = string.Empty;

    public short BusType { get; private set; } = default;
    public short MediaType { get; private set; } = default;
    public bool IsSystemDisk { get; private set; } = default;

    public List<Win32DiskPartition> Partitions { get; } = new List<Win32DiskPartition>();
}
