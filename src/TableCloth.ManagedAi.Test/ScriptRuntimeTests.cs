using System.IO.Compression;
using System.Runtime.InteropServices;
using TableCloth.ManagedAi.Windows;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class ScriptRuntimeTests
{
    [TestMethod]
    public async Task InvalidPendingAclJournalBlocksRepairBeforeDownloadingAndPreservesRecoveryEvidence()
    {
        using var fixture = new InstallFixture();
        var root = fixture.Paths.Under("skill-execution");
        Directory.CreateDirectory(root);
        var journal = Path.Combine(root, ".acl-journal-invalid.json");
        File.WriteAllText(journal, "{}");

        var status = await new WindowsManagedAiScriptRuntimeManager(fixture.Paths).EnsureInstalledAsync(CancellationToken.None);

        Assert.IsFalse(status.Python.Installed);
        Assert.IsFalse(status.Node.Installed);
        Assert.AreEqual("BlockedByPolicy", status.Python.Failure);
        Assert.IsTrue(File.Exists(journal));
        Assert.IsFalse(Directory.Exists(Path.Combine(root, "archives")));
    }

    [TestMethod]
    [DataRow(Architecture.X64, "amd64", "x64")]
    [DataRow(Architecture.Arm64, "arm64", "arm64")]
    public void OfficialPinnedPackagesCoverBothReleaseArchitectures(Architecture architecture, string pythonTarget, string nodeTarget)
    {
        var packages = ScriptRuntimePackages.ForArchitecture(architecture);
        Assert.AreEqual($"https://www.python.org/ftp/python/3.14.9/python-3.14.9-embeddable-{pythonTarget}.zip", packages.Python.Url.AbsoluteUri);
        Assert.AreEqual($"https://nodejs.org/dist/v24.22.1/node-v24.22.1-win-{nodeTarget}.zip", packages.Node.Url.AbsoluteUri);
        Assert.AreEqual(64, packages.Python.Sha256.Length);
        Assert.AreEqual(64, packages.Node.Sha256.Length);
        Assert.IsTrue(packages.Node.Includes("node.exe"));
        Assert.IsFalse(packages.Node.Includes("node_modules/npm/bin/npm-cli.js"));
    }

    [TestMethod]
    [DataRow("../escape.exe")]
    [DataRow("/absolute.exe")]
    [DataRow("C:/ads.exe")]
    [DataRow("NUL.exe")]
    public async Task RuntimeZipCannotEscapeItsOwnedDirectory(string name)
    {
        using var fixture = new InstallFixture();
        using var archive = new MemoryStream();
        using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var output = new StreamWriter(zip.CreateEntry(name).Open());
            output.Write("executable bytes");
        }
        archive.Position = 0;
        var directory = fixture.Paths.Under("archive-stage");
        Directory.CreateDirectory(directory);
        var error = await Assert.ThrowsExactlyAsync<ManagedAiException>(() =>
            SafeScriptRuntimeZip.ExtractAsync(archive, directory, "", new(), CancellationToken.None));
        Assert.AreEqual(AiFailureCode.ArchiveRejected, error.Code);
        Assert.IsEmpty(Directory.GetFileSystemEntries(directory));
    }

    [TestMethod]
    public async Task RuntimeZipRejectsLinksBeforeCreatingFiles()
    {
        using var fixture = new InstallFixture();
        using var archive = new MemoryStream();
        using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("python.exe");
            entry.ExternalAttributes = unchecked((int)0xA0000000);
            using var output = new StreamWriter(entry.Open());
            output.Write("link target");
        }
        archive.Position = 0;
        var directory = fixture.Paths.Under("archive-stage");
        Directory.CreateDirectory(directory);
        var error = await Assert.ThrowsExactlyAsync<ManagedAiException>(() =>
            SafeScriptRuntimeZip.ExtractAsync(archive, directory, "", new(), CancellationToken.None));
        Assert.AreEqual(AiFailureCode.ArchiveRejected, error.Code);
        Assert.IsEmpty(Directory.GetFileSystemEntries(directory));
    }
}
