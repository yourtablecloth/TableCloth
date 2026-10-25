using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TableCloth.ManagedAi.OpenAi;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class SkillRuntimeTests
{
    [TestMethod]
    public void ManifestPinsBothArchitecturesAndRejectsUntrustedCoordinates()
    {
        foreach (var architecture in new[] { Architecture.X64, Architecture.Arm64 })
        {
            var manifest = SkillRuntimeManifest.ForArchitecture(architecture);
            manifest.Validate();
            Assert.AreEqual("24.22.1", manifest.NodeJs.Version);
            Assert.AreEqual("3.14.9", manifest.Python.Version);
            Assert.AreEqual("11.19.1", manifest.NpmVersion);
            Assert.AreEqual(64, manifest.NodeJs.Sha256.Length);
            Assert.AreEqual(64, manifest.Python.Sha256.Length);
            Assert.AreEqual("python314", manifest.PythonStem);
            Assert.ThrowsExactly<ManagedAiException>(() => (manifest with { NodeJs = manifest.NodeJs with { Url = new("https://example.com/archive.zip") } }).Validate());
            Assert.ThrowsExactly<ManagedAiException>(() => (manifest with { Python = manifest.Python with { Target = "win-x86" } }).Validate());
            Assert.ThrowsExactly<ManagedAiException>(() => (manifest with { NodeJs = manifest.NodeJs with { Sha256 = "invalid" } }).Validate());
        }
        Assert.AreEqual(AiFailureCode.UnsupportedArchitecture,
            Assert.ThrowsExactly<ManagedAiException>(() => SkillRuntimeManifest.ForArchitecture(Architecture.X86)).Code);
    }

    [TestMethod]
    public async Task InstallationActivatesOnlyTheVerifiedPairAndIsIdempotentWithoutChangingParentEnvironment()
    {
        using var fixture = new Fixture();
        var parentPath = Environment.GetEnvironmentVariable("PATH");
        var parentCodexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        Assert.IsNull(await fixture.Manager.GetActiveAsync(CancellationToken.None));
        var stages = new List<string>();
        var installed = await fixture.Manager.InstallAsync(new ImmediateProgress(stages), CancellationToken.None);
        Assert.AreEqual("24.22.1", installed.NodeJs.Version);
        Assert.AreEqual("3.14.9", installed.Python.Version);
        Assert.AreEqual(installed, await fixture.Manager.GetActiveAsync(CancellationToken.None));
        Assert.AreEqual(parentPath, Environment.GetEnvironmentVariable("PATH"));
        Assert.AreEqual(parentCodexHome, Environment.GetEnvironmentVariable("CODEX_HOME"));
        CollectionAssert.AreEqual(File.ReadAllBytes(installed.Python.EntryPoint), File.ReadAllBytes(Path.Combine(installed.Python.RootDirectory, "python3.exe")));
        Assert.AreEqual("python314.zip\n.\n#import site\n", File.ReadAllText(Path.Combine(installed.Python.RootDirectory, "python314._pth")));
        Assert.HasCount(2, fixture.Http.Requests);
        Assert.HasCount(8, fixture.Runner.Invocations);
        Assert.Contains("SkillRuntimeActivating", stages);
        using (fixture.Paths.AcquireOperation()) Assert.IsNotNull(await fixture.Manager.GetActiveAsync(CancellationToken.None));
        await fixture.Manager.InstallAsync(null, CancellationToken.None);
        Assert.HasCount(2, fixture.Http.Requests);
        Assert.HasCount(12, fixture.Runner.Invocations);
        Assert.HasCount(1, Directory.GetDirectories(fixture.Paths.SkillRuntimeReleases));
        Assert.IsEmpty(Directory.GetFileSystemEntries(fixture.Paths.SkillRuntimeCache));
        foreach (var spec in fixture.Runner.Invocations)
        {
            Assert.IsTrue(spec.StartInfo.FileName.StartsWith(fixture.Paths.Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(spec.StartInfo.UseShellExecute);
            Assert.IsFalse(spec.StartInfo.Environment.ContainsKey("OPENAI_API_KEY"));
            Assert.IsFalse(spec.StartInfo.Environment.ContainsKey("PYTHONPATH"));
            Assert.IsFalse(spec.StartInfo.Environment.ContainsKey("NODE_OPTIONS"));
            Assert.IsTrue(spec.StartInfo.Environment["TEMP"]!.StartsWith(fixture.Paths.SkillToolData, StringComparison.OrdinalIgnoreCase));
            Assert.AreNotEqual(spec.StartInfo.Environment["NPM_CONFIG_USERCONFIG"], spec.StartInfo.Environment["NPM_CONFIG_GLOBALCONFIG"]);
        }
    }

    [TestMethod]
    [DataRow("python-network")]
    [DataRow("checksum")]
    [DataRow("version")]
    [DataRow("activation")]
    [DataRow("cancel")]
    public async Task FailedPairReplacementPreservesThePreviousActivePairAndCleansOwnedTemporaryFiles(string failure)
    {
        using var fixture = new Fixture();
        var first = await fixture.Manager.InstallAsync(null, CancellationToken.None);
        var previousState = File.ReadAllText(fixture.Paths.SkillRuntimeCurrent);
        fixture.Configure("24.22.2", "3.14.10");
        using var cancellation = new CancellationTokenSource();
        if (failure == "python-network") fixture.Http.FailPython = true;
        if (failure == "checksum") fixture.Http.CorruptNode = true;
        if (failure == "version") fixture.Runner.IncorrectVersion = true;
        if (failure == "activation") fixture.Runner.FailureOnFinalRoot = true;
        if (failure == "cancel") fixture.Http.OnPython = () => cancellation.Cancel();
        if (failure == "cancel")
            await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Manager.InstallAsync(null, cancellation.Token));
        else await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Manager.InstallAsync(null, cancellation.Token));
        Assert.AreEqual(previousState, File.ReadAllText(fixture.Paths.SkillRuntimeCurrent));
        Assert.AreEqual(first, await fixture.Manager.GetActiveAsync(CancellationToken.None));
        Assert.HasCount(1, Directory.GetDirectories(fixture.Paths.SkillRuntimeReleases));
        Assert.IsEmpty(Directory.GetFileSystemEntries(fixture.Paths.SkillRuntimeCache));
    }

    [TestMethod]
    public async Task SuccessfulPairReplacementPreservesTheOldReleaseUntilAtomicActivation()
    {
        using var fixture = new Fixture();
        var old = await fixture.Manager.InstallAsync(null, CancellationToken.None);
        var stateBefore = File.ReadAllText(fixture.Paths.SkillRuntimeCurrent);
        fixture.Configure("24.22.2", "3.14.10");
        fixture.Runner.BeforeVerify = () => Assert.AreEqual(stateBefore, File.ReadAllText(fixture.Paths.SkillRuntimeCurrent));
        var next = await fixture.Manager.InstallAsync(null, CancellationToken.None);
        Assert.AreEqual("24.22.2", next.NodeJs.Version);
        Assert.AreEqual("3.14.10", next.Python.Version);
        Assert.AreEqual(next, await fixture.Manager.GetActiveAsync(CancellationToken.None));
        Assert.IsTrue(File.Exists(old.NodeJs.EntryPoint));
        Assert.HasCount(2, Directory.GetDirectories(fixture.Paths.SkillRuntimeReleases));
    }

    [TestMethod]
    public async Task OfficialNpmRelativePathFitsTheSupportedMaximumManagedRoot()
    {
        var prefix = Path.Combine(Path.GetTempPath(), "tc-tools long-" + Guid.NewGuid().ToString("N"));
        var root = prefix + new string('x', 140 - prefix.Length);
        using var fixture = new Fixture(root);
        fixture.Configure(includeLongNpmPath: true);
        var environment = await fixture.Manager.InstallAsync(null, CancellationToken.None);
        const string relative = "node_modules/npm/node_modules/@sigstore/protobuf-specs/dist/__generated__/google/protobuf/descriptor.js";
        var path = Path.Combine(environment.NodeJs.RootDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.IsGreaterThan(240, path.Length);
        Assert.AreEqual("Official npm path fixture.", File.ReadAllText(path));
        Assert.AreEqual(environment, await fixture.Manager.GetActiveAsync(CancellationToken.None));
        Assert.AreEqual(140, fixture.Paths.Root.Length);
    }

    [TestMethod]
    [DataRow("node/node.exe")]
    [DataRow("python/python3.exe")]
    [DataRow("python/python314._pth")]
    [DataRow("receipt")]
    [DataRow("state")]
    public async Task ActiveReceiptRejectsTamperedExecutablesIsolationAndMetadata(string target)
    {
        using var fixture = new Fixture();
        var installed = await fixture.Manager.InstallAsync(null, CancellationToken.None);
        var root = Directory.GetParent(installed.NodeJs.RootDirectory)!.FullName;
        if (target == "state") File.WriteAllText(fixture.Paths.SkillRuntimeCurrent, "{\"Id\":\"../escape\",\"ReceiptSha256\":\"" + new string('a', 64) + "\"}");
        else if (target == "receipt") File.AppendAllText(Path.Combine(root, "tablecloth-skill-runtime.json"), "tampered");
        else File.AppendAllText(Path.Combine(root, target), "tampered");
        await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Manager.GetActiveAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task OperationLeaseAndDownloadSizeLimitsPreventPartialActivation()
    {
        using var fixture = new Fixture();
        using (fixture.Paths.AcquireOperation())
            Assert.AreEqual(AiFailureCode.RuntimeBusy,
                (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Manager.InstallAsync(null, CancellationToken.None))).Code);
        fixture.Limits = new() { MaxDownloadBytes = 1 };
        Assert.AreEqual(AiFailureCode.OutputLimitExceeded,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Manager.InstallAsync(null, CancellationToken.None))).Code);
        Assert.IsNull(await fixture.Manager.GetActiveAsync(CancellationToken.None));
        Assert.IsFalse(File.Exists(fixture.Paths.SkillRuntimeCurrent));
        Assert.IsEmpty(Directory.GetFileSystemEntries(fixture.Paths.SkillRuntimeCache));
    }

    [TestMethod]
    public async Task RedirectIsNeverFollowedToAnotherHost()
    {
        using var fixture = new Fixture();
        fixture.Http.Redirect = true;
        Assert.AreEqual(AiFailureCode.OfficialMetadataUnavailable,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Manager.InstallAsync(null, CancellationToken.None))).Code);
        Assert.HasCount(1, fixture.Http.Requests);
        Assert.IsFalse(File.Exists(fixture.Paths.SkillRuntimeCurrent));
    }

    [TestMethod]
    [DataRow("../escape")]
    [DataRow("/absolute")]
    [DataRow("C:/escape")]
    [DataRow("file:stream")]
    [DataRow("CON.txt")]
    [DataRow("folder/trailing.")]
    [DataRow("folder\\escape")]
    public async Task ZipRejectsUnsafePaths(string name)
    {
        using var fixture = new Fixture();
        var destination = fixture.Paths.Under("zip-test");
        Directory.CreateDirectory(destination);
        using var zip = new MemoryStream(Zip([(name, "content", 0)]));
        Assert.AreEqual(AiFailureCode.ArchiveRejected,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => SafeSkillRuntimeZip.ExtractAsync(zip, destination, "", new(), CancellationToken.None))).Code);
    }

    [TestMethod]
    public async Task ZipRejectsLinksCaseCollisionsArchiveRootMismatchAndExpansionBudgets()
    {
        using var fixture = new Fixture();
        var cases = new (byte[] Archive, string Prefix, SkillRuntimeArchiveLimits Limits)[]
        {
            (Zip([("link", "target", 0xa000 << 16)]), "", new()),
            (Zip([("junction", "target", (int)FileAttributes.ReparsePoint)]), "", new()),
            (Zip([("same", "1", 0), ("SAME", "2", 0)]), "", new()),
            (Zip([("Folder/a", "1", 0), ("folder/b", "2", 0)]), "", new()),
            (Zip([("other/file", "1", 0)]), "expected", new()),
            (Zip([("file", "long content", 0)]), "", new() { MaxFileBytes = 1 }),
            (Zip([("one", "content", 0), ("two", "content", 0)]), "", new() { MaxExtractedBytes = 10 }),
            (Zip([("one", "content", 0), ("two", "content", 0)]), "", new() { MaxEntries = 1 }),
            (Zip([("bomb", new string('x', 100000), 0)]), "", new() { MaxCompressionRatio = 10 })
        };
        for (var index = 0; index < cases.Length; index++)
        {
            var destination = fixture.Paths.Under("zip-test-" + index);
            Directory.CreateDirectory(destination);
            using var archive = new MemoryStream(cases[index].Archive);
            Assert.AreEqual(AiFailureCode.ArchiveRejected,
                (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => SafeSkillRuntimeZip.ExtractAsync(archive, destination,
                    cases[index].Prefix, cases[index].Limits, CancellationToken.None))).Code);
        }
    }

    [TestMethod]
    [DataRow("import site")]
    [DataRow("../outside")]
    public async Task PythonArchiveCannotEnableSiteOrExternalImportPaths(string extra)
    {
        using var fixture = new Fixture();
        fixture.Configure(pythonPathExtra: extra);
        Assert.AreEqual(AiFailureCode.BlockedByPolicy,
            (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Manager.InstallAsync(null, CancellationToken.None))).Code);
        Assert.IsFalse(File.Exists(fixture.Paths.SkillRuntimeCurrent));
    }

    [TestMethod]
    public async Task ZipRejectsAReparsePointInItsDestination()
    {
        using var fixture = new Fixture();
        var outside = fixture.Paths.Under("outside");
        var link = fixture.Paths.Under("linked");
        Directory.CreateDirectory(outside);
        try { Directory.CreateSymbolicLink(link, outside); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        { Assert.Inconclusive("Creating a directory symbolic link is unavailable."); }
        try
        {
            using var zip = new MemoryStream(Zip([("file", "content", 0)]));
            Assert.AreEqual(AiFailureCode.InvalidPath,
                (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => SafeSkillRuntimeZip.ExtractAsync(zip, link, "", new(), CancellationToken.None))).Code);
            Assert.IsEmpty(Directory.GetFiles(outside));
        }
        finally { if (Directory.Exists(link)) Directory.Delete(link); }
    }

    private static byte[] Zip((string Name, string Content, int Attributes)[] files)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(file.Name, CompressionLevel.Optimal);
                entry.ExternalAttributes = file.Attributes;
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(file.Content);
            }
        return buffer.ToArray();
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture(string? root = null)
        {
            Paths = new(root ?? Path.Combine(Path.GetTempPath(), "tc-tools-" + Guid.NewGuid().ToString("N")));
            Configure();
        }
        public ManagedAiPaths Paths { get; }
        public HttpFixture Http { get; } = new();
        public VersionRunner Runner { get; } = new();
        public SkillRuntimeManifest Manifest = null!;
        public SkillRuntimeArchiveLimits Limits = new();
        public SkillRuntimeManager Manager => new(Paths, Http, Runner, Manifest, Limits);
        public void Configure(string nodeVersion = "24.22.1", string pythonVersion = "3.14.9", string? pythonPathExtra = null,
            bool includeLongNpmPath = false)
        {
            const string target = "win-x64";
            var prefix = $"node-v{nodeVersion}-{target}";
            List<(string Name, string Content, int Attributes)> nodeFiles = [
                (prefix + "/node.exe", "Node executable fixture.", 0),
                (prefix + "/npm.cmd", "npm fixture.", 0), (prefix + "/npx.cmd", "npx fixture.", 0),
                (prefix + "/node_modules/npm/bin/npm-cli.js", "npm CLI fixture.", 0),
                (prefix + "/node_modules/npm/bin/npx-cli.js", "npx CLI fixture.", 0),
                (prefix + "/node_modules/npm/package.json", "{\"version\":\"11.19.1\"}", 0)
            ];
            if (includeLongNpmPath)
                nodeFiles.Add((prefix + "/node_modules/npm/node_modules/@sigstore/protobuf-specs/dist/__generated__/google/protobuf/descriptor.js", "Official npm path fixture.", 0));
            var node = Zip(nodeFiles.ToArray());
            var parts = pythonVersion.Split('.');
            var stem = "python" + parts[0] + parts[1];
            var python = Zip([
                ("python.exe", "Python executable fixture.", 0), ("python3.dll", "Python stable DLL fixture.", 0),
                (stem + ".dll", "Python DLL fixture.", 0), (stem + ".zip", "Python stdlib fixture.", 0),
                (stem + "._pth", stem + ".zip\n.\n#import site\n" + (pythonPathExtra is null ? "" : pythonPathExtra + "\n"), 0)
            ]);
            Manifest = new(new(nodeVersion, target, Convert.ToHexStringLower(SHA256.HashData(node)), new($"https://nodejs.org/dist/v{nodeVersion}/{prefix}.zip"), prefix),
                new(pythonVersion, target, Convert.ToHexStringLower(SHA256.HashData(python)), new($"https://www.python.org/ftp/python/{pythonVersion}/python-{pythonVersion}-embed-amd64.zip"), ""));
            Http.Packages[Manifest.NodeJs.Url.AbsoluteUri] = node;
            Http.Packages[Manifest.Python.Url.AbsoluteUri] = python;
            Runner.NodeVersion = nodeVersion; Runner.PythonVersion = pythonVersion;
        }
        public void Dispose()
        {
            if (Directory.Exists(Paths.Root)) Directory.Delete(Paths.Root, recursive: true);
            Http.Dispose();
        }
    }

    private sealed class VersionRunner : IJsonlProcessRunner
    {
        public string NodeVersion = "";
        public string PythonVersion = "";
        public bool IncorrectVersion;
        public bool FailureOnFinalRoot;
        public Action? BeforeVerify;
        public List<ProcessRunSpec> Invocations { get; } = [];
        public Task<ProcessOutcome> RunAsync(ProcessRunSpec spec, Action<string> stdout, Action<string>? stderr, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            BeforeVerify?.Invoke();
            Invocations.Add(spec);
            var arguments = spec.StartInfo.ArgumentList;
            var isNode = Path.GetFileName(spec.StartInfo.FileName) == "node.exe";
            var version = isNode ? arguments.Count == 1 ? "v" + NodeVersion : "11.19.1" : "Python " + PythonVersion;
            if (IncorrectVersion || FailureOnFinalRoot && !spec.StartInfo.FileName.Contains(".staging-", StringComparison.Ordinal)) version = "wrong version";
            stdout(version);
            return Task.FromResult(new ProcessOutcome(0, version.Length, 0));
        }
    }
    private sealed class HttpFixture : HttpMessageHandler, IHttpClientFactory
    {
        public Dictionary<string, byte[]> Packages { get; } = [];
        public List<string> Requests { get; } = [];
        public bool Redirect;
        public bool FailPython;
        public bool CorruptNode;
        public Action? OnPython;
        public HttpClient CreateClient(string name)
        {
            Assert.AreEqual(SkillRuntimeManager.HttpClientName, name);
            return new(this, disposeHandler: false);
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var url = request.RequestUri!.AbsoluteUri;
            Requests.Add(url);
            if (Redirect) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new("https://example.com/payload.zip") } });
            if (url.Contains("python.org", StringComparison.Ordinal))
            {
                OnPython?.Invoke();
                if (FailPython) throw new HttpRequestException();
            }
            var data = Packages[url];
            if (CorruptNode && url.Contains("nodejs.org", StringComparison.Ordinal)) data = "corrupt archive"u8.ToArray();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) });
        }
    }
    private sealed class ImmediateProgress(List<string> stages) : IProgress<AiProgress>
    {
        public void Report(AiProgress value) => stages.Add(value.Stage);
    }
}
