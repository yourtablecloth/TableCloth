using System.ComponentModel;

namespace TableCloth.AppContainerPoc;

internal sealed class OwnedPaths
{
    internal string Root { get; }

    internal OwnedPaths(string root)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10)) throw new PlatformNotSupportedException();
        if (!Path.IsPathFullyQualified(root) || root.StartsWith("\\\\", StringComparison.Ordinal))
            throw new ArgumentException("A local absolute owned root is required.");
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (Root == Path.GetPathRoot(Root) || !Directory.Exists(Root))
            throw new ArgumentException("The dedicated owned root must already exist.");
        Check(Root, allowRoot: true);
    }

    internal string Check(string path, bool allowRoot = false)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith("\\\\", StringComparison.Ordinal))
            throw new ArgumentException("Only local absolute owned paths are permitted.");
        path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (path.AsSpan(2).Contains(':') || path.Split(Path.DirectorySeparatorChar).Skip(1)
            .Any(segment => segment.EndsWith('.') || segment.EndsWith(' ')))
            throw new ArgumentException("Alternate streams and ambiguous Win32 path segments are not permitted.");
        if (!(allowRoot && path.Equals(Root, StringComparison.OrdinalIgnoreCase)) &&
            !path.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A path is outside the dedicated owned root.");
        for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
            if ((File.Exists(current.FullName) || Directory.Exists(current.FullName)) &&
                (File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Reparse points are not permitted.");
        return path;
    }

    internal FileStream AcquireAclLease()
    {
        var path = Check(Path.Combine(Root, ".appcontainer-acl.lock"));
        if (File.Exists(path)) Identity(path);
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    internal Win32.FileInformation Identity(string path)
    {
        Check(path, allowRoot: true);
        using var handle = Win32.OpenMetadata(path, 0, 7, 0, 3, 0x02200000, 0);
        if (handle.IsInvalid) throw new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastPInvokeError());
        Win32.Check(Win32.GetFileInformationByHandle(handle, out var identity));
        if ((identity.Attributes & (uint)FileAttributes.ReparsePoint) != 0 ||
            (identity.Attributes & (uint)FileAttributes.Directory) == 0 && identity.Links != 1)
            throw new IOException("Reparse points and hard-linked files are not permitted.");
        return identity;
    }

    internal void DeleteRunDirectory(string path)
    {
        Check(path);
        if (!Directory.Exists(path)) return;
        DeleteContents(path);
        Directory.Delete(path);
    }

    private static void DeleteContents(string directory)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.Directory) != 0)
            {
                if ((attributes & FileAttributes.ReparsePoint) == 0) DeleteContents(entry);
                Directory.Delete(entry);
            }
            else
            {
                if ((attributes & FileAttributes.ReadOnly) != 0 && (attributes & FileAttributes.ReparsePoint) == 0)
                    File.SetAttributes(entry, attributes & ~FileAttributes.ReadOnly);
                File.Delete(entry);
            }
        }
    }
}
