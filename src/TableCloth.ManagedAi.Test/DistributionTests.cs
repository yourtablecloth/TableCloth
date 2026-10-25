using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TableCloth.ManagedAi.OpenAi;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class DistributionTests
{
    [TestMethod]
    public void SelectsX64AndArm64AndRejectsAmbiguity()
    {
        using var fixture = new InstallFixture();
        var json = fixture.Metadata();
        Assert.AreEqual("x86_64-pc-windows-msvc", OpenAiReleaseMetadataClient.Parse(json, Architecture.X64).Target);
        Assert.AreEqual("aarch64-pc-windows-msvc", OpenAiReleaseMetadataClient.Parse(json, Architecture.Arm64).Target);
        Assert.ThrowsExactly<ManagedAiException>(() => OpenAiReleaseMetadataClient.Parse(json, Architecture.X86));
        var malicious = Encoding.UTF8.GetString(json).Replace("releases.openai.com", "attacker.example");
        Assert.ThrowsExactly<ManagedAiException>(() => OpenAiReleaseMetadataClient.Parse(Encoding.UTF8.GetBytes(malicious), Architecture.X64));
        var duplicate = fixture.Metadata(duplicate: true);
        Assert.ThrowsExactly<ManagedAiException>(() => OpenAiReleaseMetadataClient.Parse(duplicate, Architecture.X64));
    }

    [TestMethod]
    public async Task RedirectCannotLeaveOfficialHost()
    {
        var requests = new List<Uri>();
        using var factory = new TestHttpFactory(request =>
        {
            requests.Add(request.RequestUri!);
            return new(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://attacker.example/payload") } };
        });
        var metadata = new OpenAiReleaseMetadataClient(factory, new());
        using var target = new MemoryStream();
        await Assert.ThrowsExactlyAsync<ManagedAiException>(() => metadata.DownloadAsync(new(OpenAiReleaseMetadataClient.Latest), target, 100, CancellationToken.None));
        Assert.HasCount(1, requests);
    }

    [TestMethod]
    public async Task InstallIsIdempotentAndRollbackPreservesProfile()
    {
        using var fixture = new InstallFixture();
        var first = await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        Assert.AreEqual("1.2.3", first.Coordinate.Version);
        var downloads = fixture.PackageDownloads;
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        Assert.AreEqual(downloads, fixture.PackageDownloads);
        File.WriteAllText(Path.Combine(fixture.Paths.Profile, "sentinel"), "profile");
        fixture.Version = "1.2.4";
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var rolledBack = await fixture.Installer.RollbackAsync(CancellationToken.None);
        Assert.AreEqual("1.2.3", rolledBack.Coordinate.Version);
        Assert.AreEqual("profile", File.ReadAllText(Path.Combine(fixture.Paths.Profile, "sentinel")));
    }

    [TestMethod]
    [DataRow("package")]
    [DataRow("checksums")]
    [DataRow("network")]
    [DataRow("activation")]
    public async Task FailedUpdateKeepsExistingVersionAndCleansTemporaryFiles(string failure)
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var before = File.ReadAllText(fixture.Paths.Current);
        fixture.Version = "1.2.4";
        fixture.Failure = failure;
        fixture.Runner.VersionCalls = 0;
        await Assert.ThrowsAsync<Exception>(() => fixture.Installer.InstallAsync(null, null, CancellationToken.None));
        Assert.AreEqual(before, File.ReadAllText(fixture.Paths.Current));
        Assert.AreEqual("1.2.3", (await fixture.Installer.GetActiveAsync(CancellationToken.None))!.Coordinate.Version);
        Assert.HasCount(0, Directory.GetFiles(fixture.Paths.Downloads));
        Assert.HasCount(0, Directory.GetDirectories(fixture.Paths.Releases, "*.staging.*"));
    }

    [TestMethod]
    [DataRow("../escape", TarEntryType.RegularFile)]
    [DataRow("/absolute", TarEntryType.RegularFile)]
    [DataRow("C:/absolute", TarEntryType.RegularFile)]
    [DataRow("file:stream", TarEntryType.RegularFile)]
    [DataRow("CON.txt", TarEntryType.RegularFile)]
    [DataRow("folder/trailing.", TarEntryType.RegularFile)]
    [DataRow("link", TarEntryType.SymbolicLink)]
    [DataRow("hardlink", TarEntryType.HardLink)]
    public async Task RejectsDangerousTarEntries(string name, TarEntryType type)
    {
        using var fixture = new InstallFixture();
        var staging = fixture.Paths.Under("test-staging");
        Directory.CreateDirectory(staging);
        using var archive = new MemoryStream(CreateArchive([(name, type)]));
        await Assert.ThrowsExactlyAsync<ManagedAiException>(() => SafeCodexArchive.ExtractAsync(archive, staging, new(), CancellationToken.None));
    }

    [TestMethod]
    public async Task RejectsDuplicateEntriesAndExpansionLimits()
    {
        using var fixture = new InstallFixture();
        var staging = fixture.Paths.Under("test-staging");
        Directory.CreateDirectory(staging);
        using var duplicate = new MemoryStream(CreateArchive([("file", TarEntryType.RegularFile), ("FILE", TarEntryType.RegularFile)]));
        await Assert.ThrowsExactlyAsync<ManagedAiException>(() => SafeCodexArchive.ExtractAsync(duplicate, staging, new(), CancellationToken.None));
        using var large = new MemoryStream(CreateArchive([("other", TarEntryType.RegularFile)]));
        await Assert.ThrowsExactlyAsync<ManagedAiException>(() => SafeCodexArchive.ExtractAsync(large, staging, new() { MaxExtractedBytes = 1 }, CancellationToken.None));
        using var entries = new MemoryStream(CreateArchive([("one", TarEntryType.RegularFile), ("two", TarEntryType.RegularFile)]));
        await Assert.ThrowsExactlyAsync<ManagedAiException>(() => SafeCodexArchive.ExtractAsync(entries, staging, new() { MaxArchiveEntries = 1 }, CancellationToken.None));
    }

    [TestMethod]
    public void ChecksumListRejectsDuplicateAndConflictingDigest()
    {
        var asset = new ReleaseAsset("package.tar.gz", new string('a', 64), new("https://releases.openai.com/codex/package.tar.gz"));
        var line = asset.Sha256 + "  " + asset.Name + "\n";
        OpenAiCodexPackageInstaller.VerifyChecksumList(line, asset);
        Assert.ThrowsExactly<ManagedAiException>(() => OpenAiCodexPackageInstaller.VerifyChecksumList(line + line, asset));
        Assert.ThrowsExactly<ManagedAiException>(() => OpenAiCodexPackageInstaller.VerifyChecksumList(new string('b', 64) + "  " + asset.Name, asset));
    }

    [TestMethod]
    public void OperationLeasePreventsConcurrentMutation()
    {
        using var fixture = new InstallFixture();
        using var lease = fixture.Paths.AcquireOperation();
        Assert.AreEqual(AiFailureCode.RuntimeBusy, Assert.ThrowsExactly<ManagedAiException>(() => fixture.Paths.AcquireOperation()).Code);
    }

    internal static byte[] CreateArchive((string Name, TarEntryType Type)[] entries)
    {
        using var result = new MemoryStream();
        using (var gzip = new GZipStream(result, CompressionLevel.Fastest, leaveOpen: true))
        using (var tar = new TarWriter(gzip, leaveOpen: true))
        {
            foreach (var (name, type) in entries)
            {
                var entry = new PaxTarEntry(type, name);
                if (type == TarEntryType.RegularFile) entry.DataStream = new MemoryStream("fixture content"u8.ToArray());
                if (type is TarEntryType.HardLink or TarEntryType.SymbolicLink) entry.LinkName = "../outside";
                tar.WriteEntry(entry);
                entry.DataStream?.Dispose();
            }
        }
        return result.ToArray();
    }
}

internal sealed class TestHttpFactory(Func<HttpRequestMessage, HttpResponseMessage> response) : IHttpClientFactory, IDisposable
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response(request));
    }
    private readonly Handler _handler = new(response);
    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    public void Dispose() => _handler.Dispose();
}

internal sealed class InstallFixture : IDisposable
{
    public readonly ManagedAiPaths Paths = new(Path.Combine(Path.GetTempPath(), "tc-ai-test-" + Guid.NewGuid().ToString("N")));
    public string Version = "1.2.3";
    public string? Failure;
    public int PackageDownloads;
    public readonly FakeVersionRunner Runner;
    public readonly OpenAiCodexPackageInstaller Installer;
    private readonly byte[] _archive = DistributionTests.CreateArchive(SafeCodexArchive.RequiredFiles.Select(x => (x, TarEntryType.RegularFile)).ToArray());
    private readonly TestHttpFactory _factory;

    public InstallFixture()
    {
        Runner = new FakeVersionRunner(this);
        _factory = new TestHttpFactory(Respond);
        Installer = new(Paths, new(_factory, new()), Runner, new FakeProfile(Paths), new());
    }
    private byte[] Checksums() => Encoding.UTF8.GetBytes(string.Join('\n', new[] { "x86_64", "aarch64" }.Select(arch =>
        Hash(_archive) + "  codex-package-" + arch + "-pc-windows-msvc.tar.gz")) + "\n");
    public byte[] Metadata(bool duplicate = false)
    {
        object Asset(string name, byte[] content) => new { name, digest = "sha256:" + Hash(content),
            browser_download_url = $"https://releases.openai.com/codex/releases/{Version}/{name}" };
        var assets = new List<object>
        {
            Asset("codex-package-x86_64-pc-windows-msvc.tar.gz", _archive),
            Asset("codex-package-aarch64-pc-windows-msvc.tar.gz", _archive),
            Asset("codex-package_SHA256SUMS", Checksums())
        };
        if (duplicate) assets.Add(assets[0]);
        return JsonSerializer.SerializeToUtf8Bytes(new { tag_name = "rust-v" + Version, assets });
    }
    private HttpResponseMessage Respond(HttpRequestMessage request)
    {
        var url = request.RequestUri!.AbsolutePath;
        if (url.EndsWith("latest") || url.EndsWith("release.json")) return Ok(Metadata());
        if (url.EndsWith("SHA256SUMS")) return Ok(Failure == "checksums" ? "tampered"u8.ToArray() : Checksums());
        PackageDownloads++;
        if (Failure == "network") throw new HttpRequestException();
        return Ok(Failure == "package" ? [.. _archive, 1] : _archive);
    }
    private static HttpResponseMessage Ok(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public void Dispose()
    {
        _factory.Dispose();
        // This unique test root is the explicitly owned directory for this fixture.
        ManagedAiPaths.RejectReparsePoints(Paths.Root);
        if (Directory.Exists(Paths.Root)) Directory.Delete(Paths.Root, recursive: true);
    }
    private sealed class FakeProfile(ManagedAiPaths paths) : IManagedAiProfile
    {
        public void Prepare() => Directory.CreateDirectory(paths.Profile);
    }
    public sealed class FakeVersionRunner(InstallFixture fixture) : IJsonlProcessRunner
    {
        public int VersionCalls;
        public Task<ProcessOutcome> RunAsync(ProcessRunSpec spec, Action<string> stdout, Action<string>? stderr, CancellationToken cancellationToken)
        {
            VersionCalls++;
            if (fixture.Failure == "activation" && VersionCalls == 3) throw new ManagedAiException(AiFailureCode.RuntimeVersionMismatch);
            var version = spec.StartInfo.FileName.Contains("1.2.4", StringComparison.Ordinal) ? "1.2.4" : "1.2.3";
            stdout("codex-cli " + version);
            return Task.FromResult(new ProcessOutcome(0, 16, 0));
        }
    }
}
