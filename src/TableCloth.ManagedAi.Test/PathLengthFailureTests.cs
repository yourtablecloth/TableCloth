using System.Runtime.InteropServices;
using TableCloth.ManagedAi.OpenAi;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class PathLengthFailureTests
{
    [TestMethod]
    [DataRow(true, AiFailureCode.PathLengthUnsupported)]
    [DataRow(false, AiFailureCode.ArchiveRejected)]
    public async Task ArchiveDistinguishesPathLengthFromOtherIoFailures(bool pathTooLong, AiFailureCode expected)
    {
        using var fixture = new OwnedFixture();
        using var archive = new FaultingArchiveStream(pathTooLong);
        var error = await Assert.ThrowsExactlyAsync<ManagedAiException>(() => SafeSkillRuntimeZip.ExtractAsync(
            archive, fixture.Paths.Root, "", new(), CancellationToken.None));
        Assert.AreEqual(expected, error.Code);
        Assert.AreEqual(expected.ToString(), error.Message);
        Assert.IsNull(error.InnerException);
    }

    [TestMethod]
    public async Task MalformedArchiveKeepsItsArchiveFailureCode()
    {
        using var fixture = new OwnedFixture();
        using var archive = new MemoryStream("not a ZIP archive"u8.ToArray());
        var error = await Assert.ThrowsExactlyAsync<ManagedAiException>(() => SafeSkillRuntimeZip.ExtractAsync(
            archive, fixture.Paths.Root, "", new(), CancellationToken.None));
        Assert.AreEqual(AiFailureCode.ArchiveRejected, error.Code);
    }

    [TestMethod]
    [DataRow("path", AiFailureCode.PathLengthUnsupported)]
    [DataRow("io", AiFailureCode.OfficialMetadataUnavailable)]
    [DataRow("missing", AiFailureCode.OfficialMetadataUnavailable)]
    public async Task InstallationDistinguishesPathLengthAndCleansUncommittedFiles(string failure, AiFailureCode expected)
    {
        using var fixture = new OwnedFixture();
        using var http = new TestHttpFactory(_ => throw (failure switch
        {
            "path" => new PathTooLongException("Private filesystem details."),
            "missing" => new FileNotFoundException("Private filesystem details."),
            _ => new IOException("Private filesystem details.")
        }));
        var manager = new SkillRuntimeManager(fixture.Paths, http, new UnusedRunner(),
            SkillRuntimeManifest.ForArchitecture(Architecture.X64));
        var error = await Assert.ThrowsExactlyAsync<ManagedAiException>(() => manager.InstallAsync(null, CancellationToken.None));
        Assert.AreEqual(expected, error.Code);
        Assert.AreEqual(expected.ToString(), error.Message);
        Assert.IsNull(error.InnerException);
        Assert.IsNull(await manager.GetActiveAsync(CancellationToken.None));
        Assert.IsFalse(File.Exists(fixture.Paths.SkillRuntimeCurrent));
        Assert.IsEmpty(Directory.GetFileSystemEntries(fixture.Paths.SkillRuntimeReleases));
        Assert.IsEmpty(Directory.GetFileSystemEntries(fixture.Paths.SkillRuntimeCache));
    }

    private sealed class FaultingArchiveStream(bool pathTooLong) : MemoryStream(new byte[22])
    {
        public override int Read(Span<byte> buffer) => throw Failure();
        public override int Read(byte[] buffer, int offset, int count) => throw Failure();
        private IOException Failure() => pathTooLong
            ? new PathTooLongException("Private filesystem details.")
            : new IOException("Private filesystem details.");
    }

    private sealed class UnusedRunner : IJsonlProcessRunner
    {
        public Task<ProcessOutcome> RunAsync(ProcessRunSpec spec, Action<string> stdout, Action<string>? stderr,
            CancellationToken cancellationToken) => throw new AssertFailedException("Unverified runtime was executed.");
    }

    private sealed class OwnedFixture : IDisposable
    {
        public ManagedAiPaths Paths { get; } = new(Path.Combine(Path.GetTempPath(), "tc-path-" + Guid.NewGuid().ToString("N")));
        public void Dispose()
        {
            ManagedAiPaths.RejectReparsePoints(Paths.Root);
            if (Directory.Exists(Paths.Root)) Directory.Delete(Paths.Root, recursive: true);
        }
    }
}
