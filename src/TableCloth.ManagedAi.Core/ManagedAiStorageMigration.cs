using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Security.Principal;

[assembly: InternalsVisibleTo("TableCloth.ManagedAi.Test")]

namespace TableCloth.ManagedAi;

// User data must remain outside the Velopack installation root. A full Setup
// reinstall replaces that whole root, including directories unknown to Velopack.
// This can move a surviving legacy store; it cannot recover a store which an
// older Setup has already removed. Such upgrades require a pre-install move.
internal static class ManagedAiStorageMigration
{
    private const string Marker = ".tablecloth-managed-ai";
    private static readonly object Gate = new();

    internal static ManagedAiPaths ForCurrentUser() => Resolve(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    // The production entry point only supplies this account's known LocalAppData.
    // A separate internal seam lets tests use an explicitly owned temporary root.
    internal static ManagedAiPaths Resolve(string localApplicationData)
    {
        lock (Gate)
        {
            var legacy = new ManagedAiPaths(Path.Combine(localApplicationData, "TableCloth", "ManagedAi"));
            var destination = new ManagedAiPaths(Path.Combine(localApplicationData, "TableCloth.Data", "ManagedAi"));
            if (!Directory.Exists(legacy.Root)) return destination;
            if (!Directory.EnumerateFileSystemEntries(legacy.Root).Any()) return destination;
            if (!OperatingSystem.IsWindows()) throw Blocked();

            var dataRoot = Path.GetDirectoryName(destination.Root)!;
            ManagedAiPaths.RejectReparsePoints(dataRoot);
            RequireCurrentUserOwner(legacy.Root);
            if (Directory.Exists(dataRoot)) RequireCurrentUserOwner(dataRoot);
            Directory.CreateDirectory(dataRoot);
            try
            {
                using var migrationLease = new FileStream(Path.Combine(dataRoot, ".managed-ai-migration.lock"),
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                // Recheck under the cross-process lease: another process may have
                // already completed the move while this call was waiting to enter.
                if (!Directory.Exists(legacy.Root)) return destination;
                ValidateOwnedTree(legacy.Root);
                if (File.Exists(destination.Root)) throw Blocked();
                if (Directory.Exists(destination.Root))
                {
                    RequireCurrentUserOwner(destination.Root);
                    if (Directory.EnumerateFileSystemEntries(destination.Root).Any()) throw Blocked();
                }
                var marker = Path.Combine(legacy.Root, Marker);
                if (!File.Exists(marker) || new FileInfo(marker).Length > 16 || File.ReadAllText(marker) != "1")
                    throw Blocked();
                var execution = Path.Combine(legacy.Root, "skill-execution");
                if (Directory.Exists(execution) && Directory.EnumerateFiles(execution, ".acl-journal-*.json").Any())
                    throw new ManagedAiException(AiFailureCode.RuntimeBusy);

                // Verify that legacy operations are idle, then close every
                // descendant handle before renaming the directory. Windows
                // rejects this directory rename even with FileShare.Delete.
                // The sibling migration lease remains held throughout. An old
                // binary which acquires a runtime lease in this narrow interval
                // makes Directory.Move fail safely instead of moving live data.
                using (var runtimeLease = OpenExistingLease(Path.Combine(legacy.RuntimeRoot, "install.lock")))
                using (var workerLease = OpenExistingLease(Path.Combine(execution, ".appcontainer-acl.lock")))
                {
                }
                ManagedAiPaths.RejectReparsePoints(legacy.Root);
                ManagedAiPaths.RejectReparsePoints(destination.Root);
                if (Directory.Exists(destination.Root)) Directory.Delete(destination.Root, recursive: false);
                Directory.Move(legacy.Root, destination.Root);
                return destination;
            }
            catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33)
            {
                throw new ManagedAiException(AiFailureCode.RuntimeBusy);
            }
        }
    }

    private static FileStream? OpenExistingLease(string file)
    {
        ManagedAiPaths.RejectReparsePoints(file);
        return File.Exists(file) ? new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.Delete) : null;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void RequireCurrentUserOwner(string path)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var sid = identity.User ?? throw Blocked();
        var owner = Directory.Exists(path)
            ? new DirectoryInfo(path).GetAccessControl(AccessControlSections.Owner).GetOwner(typeof(SecurityIdentifier))
            : new FileInfo(path).GetAccessControl(AccessControlSections.Owner).GetOwner(typeof(SecurityIdentifier));
        if (!sid.Equals(owner)) throw Blocked();
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void ValidateOwnedTree(string root)
    {
        var pending = new Stack<(string Path, int Depth)>();
        pending.Push((root, 0));
        var count = 0;
        while (pending.TryPop(out var directory))
        {
            if (directory.Depth > 128) throw Blocked();
            ManagedAiPaths.RejectReparsePoints(directory.Path);
            RequireCurrentUserOwner(directory.Path);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory.Path))
            {
                if (++count > 100000) throw Blocked();
                ManagedAiPaths.RejectReparsePoints(entry);
                RequireCurrentUserOwner(entry);
                if (Directory.Exists(entry)) pending.Push((entry, directory.Depth + 1));
            }
        }
    }

    private static ManagedAiException Blocked() => new(AiFailureCode.BlockedByPolicy);
}
