using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TableCloth.ManagedAi;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class ManagedAiStorageMigrationTests
{
    [TestMethod]
    public void FreshStorageUsesDataDirectoryOutsideInstallationRoot()
    {
        using var fixture = new StorageFixture();
        var paths = ManagedAiStorageMigration.Resolve(fixture.LocalAppData);
        Assert.AreEqual(fixture.Destination.Root, paths.Root);
        Assert.IsFalse(Directory.Exists(paths.Root));
        Assert.IsFalse(paths.Root.StartsWith(Path.Combine(fixture.LocalAppData, "TableCloth") + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ExistingOwnedStorageMovesAsOneDirectoryWithoutChangingProfileBytesOrOwner()
    {
        using var fixture = new StorageFixture();
        fixture.CreateLegacy();
        Directory.CreateDirectory(fixture.Legacy.Profile);
        // Synthetic bytes only. This test never reads the real account's login.
        var authentication = Encoding.UTF8.GetBytes("synthetic-profile-fixture");
        var auth = Path.Combine(fixture.Legacy.Profile, "auth.json");
        File.WriteAllBytes(auth, authentication);
        var owner = new DirectoryInfo(fixture.Legacy.Root).GetAccessControl(AccessControlSections.Owner)
            .GetOwner(typeof(SecurityIdentifier));
        Directory.CreateDirectory(fixture.Legacy.State);
        var runtimeState = "{\"Version\":\"0.156.1\",\"Target\":\"x86_64-pc-windows-msvc\",\"PackageSha256\":\"synthetic\"}";
        File.WriteAllText(fixture.Legacy.Current, runtimeState);

        var migrated = ManagedAiStorageMigration.Resolve(fixture.LocalAppData);

        Assert.IsFalse(Directory.Exists(fixture.Legacy.Root));
        CollectionAssert.AreEqual(authentication, File.ReadAllBytes(Path.Combine(migrated.Profile, "auth.json")));
        Assert.AreEqual(runtimeState, File.ReadAllText(migrated.Current));
        Assert.AreEqual(owner, new DirectoryInfo(migrated.Root).GetAccessControl(AccessControlSections.Owner)
            .GetOwner(typeof(SecurityIdentifier)));
        Assert.AreEqual(migrated.Root, ManagedAiStorageMigration.Resolve(fixture.LocalAppData).Root);
    }

    [TestMethod]
    public void ExistingEmptyDestinationAllowsOwnedDirectoryMove()
    {
        using var fixture = new StorageFixture();
        fixture.CreateLegacy();
        Directory.CreateDirectory(fixture.Destination.Root);
        ManagedAiStorageMigration.Resolve(fixture.LocalAppData);
        Assert.IsFalse(Directory.Exists(fixture.Legacy.Root));
        Assert.AreEqual("1", File.ReadAllText(Path.Combine(fixture.Destination.Root, ".tablecloth-managed-ai")));
    }

    [TestMethod]
    public void TwoPopulatedStoresNeverMergeOrOverwriteEitherProfile()
    {
        using var fixture = new StorageFixture();
        fixture.CreateLegacy();
        Directory.CreateDirectory(fixture.Destination.Profile);
        File.WriteAllText(Path.Combine(fixture.Destination.Profile, "synthetic.txt"), "destination");

        AssertFailure(AiFailureCode.BlockedByPolicy, () => ManagedAiStorageMigration.Resolve(fixture.LocalAppData));

        Assert.IsTrue(Directory.Exists(fixture.Legacy.Root));
        Assert.AreEqual("destination", File.ReadAllText(Path.Combine(fixture.Destination.Profile, "synthetic.txt")));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("0")]
    [DataRow("unexpected-marker")]
    public void MissingOrUnexpectedOwnershipMarkerPreventsMove(string? marker)
    {
        using var fixture = new StorageFixture();
        Directory.CreateDirectory(fixture.Legacy.Root);
        File.WriteAllText(Path.Combine(fixture.Legacy.Root, "synthetic.txt"), "preserved");
        if (marker is not null) File.WriteAllText(Path.Combine(fixture.Legacy.Root, ".tablecloth-managed-ai"), marker);
        AssertFailure(AiFailureCode.BlockedByPolicy, () => ManagedAiStorageMigration.Resolve(fixture.LocalAppData));
        Assert.AreEqual("preserved", File.ReadAllText(Path.Combine(fixture.Legacy.Root, "synthetic.txt")));
        Assert.IsFalse(Directory.Exists(fixture.Destination.Root));
    }

    [TestMethod]
    public void EmptyUnmarkedLegacyDirectoryIsLeftUntouched()
    {
        using var fixture = new StorageFixture();
        Directory.CreateDirectory(fixture.Legacy.Root);
        Assert.AreEqual(fixture.Destination.Root, ManagedAiStorageMigration.Resolve(fixture.LocalAppData).Root);
        Assert.IsTrue(Directory.Exists(fixture.Legacy.Root));
        Assert.IsFalse(Directory.Exists(fixture.Destination.Root));
    }

    [TestMethod]
    [DataRow("runtime")]
    [DataRow("worker")]
    public void ActiveRuntimeOrWorkerLeaseBlocksMigrationWithoutMovingData(string kind)
    {
        using var fixture = new StorageFixture();
        fixture.CreateLegacy();
        var lease = kind == "runtime" ? Path.Combine(fixture.Legacy.RuntimeRoot, "install.lock")
            : fixture.Legacy.Under("skill-execution", ".appcontainer-acl.lock");
        Directory.CreateDirectory(Path.GetDirectoryName(lease)!);
        using var active = new FileStream(lease, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        AssertFailure(AiFailureCode.RuntimeBusy, () => ManagedAiStorageMigration.Resolve(fixture.LocalAppData));
        Assert.IsTrue(Directory.Exists(fixture.Legacy.Root));
        Assert.IsFalse(Directory.Exists(fixture.Destination.Root));
    }

    [TestMethod]
    [DataRow("runtime")]
    [DataRow("worker")]
    [DataRow("both")]
    public void IdleLeaseFilesDoNotKeepDescendantHandlesOpenDuringDirectoryMove(string kind)
    {
        using var fixture = new StorageFixture();
        fixture.CreateLegacy();
        var leases = new List<string>();
        if (kind is "runtime" or "both") leases.Add(Path.Combine(fixture.Legacy.RuntimeRoot, "install.lock"));
        if (kind is "worker" or "both") leases.Add(fixture.Legacy.Under("skill-execution", ".appcontainer-acl.lock"));
        foreach (var lease in leases)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(lease)!);
            File.WriteAllText(lease, "");
        }

        var migrated = ManagedAiStorageMigration.Resolve(fixture.LocalAppData);

        Assert.IsFalse(Directory.Exists(fixture.Legacy.Root));
        foreach (var lease in leases)
            Assert.IsTrue(File.Exists(Path.Combine(migrated.Root, Path.GetRelativePath(fixture.Legacy.Root, lease))));
    }

    [TestMethod]
    public void PendingAclRecoveryIsNotMovedToAPathDifferentFromItsJournal()
    {
        using var fixture = new StorageFixture();
        fixture.CreateLegacy();
        var journal = fixture.Legacy.Under("skill-execution", ".acl-journal-synthetic.json");
        Directory.CreateDirectory(Path.GetDirectoryName(journal)!);
        File.WriteAllText(journal, "{}");
        AssertFailure(AiFailureCode.RuntimeBusy, () => ManagedAiStorageMigration.Resolve(fixture.LocalAppData));
        Assert.IsTrue(File.Exists(journal));
        Assert.IsFalse(Directory.Exists(fixture.Destination.Root));
    }

    [TestMethod]
    public void ReparseChildDoesNotMoveOrFollowAnExternalDirectory()
    {
        using var fixture = new StorageFixture();
        fixture.CreateLegacy();
        var external = Path.Combine(fixture.LocalAppData, "external-fixture");
        Directory.CreateDirectory(external);
        File.WriteAllText(Path.Combine(external, "synthetic.txt"), "untouched");
        var link = Path.Combine(fixture.Legacy.Root, "linked-fixture");
        try
        {
            try { Directory.CreateSymbolicLink(link, external); }
            catch (Exception error) when (error is UnauthorizedAccessException or IOException)
            {
                Assert.Inconclusive("This Windows account cannot create a symbolic link for the reparse-point regression.");
            }
            AssertFailure(AiFailureCode.InvalidPath, () => ManagedAiStorageMigration.Resolve(fixture.LocalAppData));
            Assert.IsTrue(Directory.Exists(fixture.Legacy.Root));
            Assert.AreEqual("untouched", File.ReadAllText(Path.Combine(external, "synthetic.txt")));
            Assert.IsFalse(Directory.Exists(fixture.Destination.Root));
        }
        finally
        {
            if (Directory.Exists(link)) Directory.Delete(link, recursive: false);
        }
    }

    [TestMethod]
    public void RecreatedInstallationDirectoryCannotRemoveMigratedUserData()
    {
        using var fixture = new StorageFixture();
        fixture.CreateLegacy();
        Directory.CreateDirectory(fixture.Legacy.Profile);
        File.WriteAllText(Path.Combine(fixture.Legacy.Profile, "synthetic.txt"), "preserved");
        var migrated = ManagedAiStorageMigration.Resolve(fixture.LocalAppData);
        // The fixture owns this exact, bounded installation directory. Simulate
        // Setup replacing it without deleting the sibling user-data directory.
        var installationRoot = Path.Combine(fixture.LocalAppData, "TableCloth");
        Assert.IsTrue(installationRoot.StartsWith(fixture.LocalAppData + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        ManagedAiPaths.RejectReparsePoints(installationRoot);
        Directory.Delete(installationRoot, recursive: true);
        Directory.CreateDirectory(installationRoot);
        Assert.AreEqual("preserved", File.ReadAllText(Path.Combine(migrated.Profile, "synthetic.txt")));
    }

    private static void AssertFailure(AiFailureCode expected, Action action)
        => Assert.AreEqual(expected, Assert.Throws<ManagedAiException>(action).Code);

    private sealed class StorageFixture : IDisposable
    {
        internal string LocalAppData { get; } = Path.Combine(Path.GetTempPath(), "tc-storage-" + Guid.NewGuid().ToString("N"));
        internal ManagedAiPaths Legacy => new(Path.Combine(LocalAppData, "TableCloth", "ManagedAi"));
        internal ManagedAiPaths Destination => new(Path.Combine(LocalAppData, "TableCloth.Data", "ManagedAi"));
        internal void CreateLegacy()
        {
            Directory.CreateDirectory(Legacy.Root);
            File.WriteAllText(Path.Combine(Legacy.Root, ".tablecloth-managed-ai"), "1");
        }
        public void Dispose()
        {
            ManagedAiPaths.RejectReparsePoints(LocalAppData);
            if (Directory.Exists(LocalAppData)) Directory.Delete(LocalAppData, recursive: true);
        }
    }
}
