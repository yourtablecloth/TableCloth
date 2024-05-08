namespace TableCloth.Components;

public interface ISystemProperties
{
    bool? IsSystemPartitionBitLockerEnabled();

    bool? IsSystemDiskAHardDrive();
}
