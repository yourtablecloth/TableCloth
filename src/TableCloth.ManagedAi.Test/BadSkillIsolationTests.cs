using System.Security.Cryptography;
using System.Text;
using TableCloth.AppContainerPoc;
using TableCloth.ManagedAi.Windows;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class BadSkillIsolationTests
{
    [TestMethod]
    [DataRow("oversized")]
    [DataRow("invalid-id")]
    [DataRow("outside-directory")]
    [DataRow("unsupported-script")]
    [DataRow("manifest-budget")]
    public async Task OneIncompatiblePackageDoesNotDisableAnotherInstalledScript(string condition)
    {
        using var fixture = new Fixture();
        var good = fixture.Install("good", new() { ["scripts/run.py"] = "print('good')" });
        var bad = fixture.Install("bad", new() { ["scripts/run.py"] = "print('bad')" });
        switch (condition)
        {
            case "oversized":
                File.WriteAllBytes(Path.Combine(bad.Directory, "large.dat"), new byte[1024 * 1024 + 1]);
                break;
            case "invalid-id": bad = bad with { Id = "bad/escape" }; break;
            case "outside-directory": bad = bad with { Directory = fixture.Paths.Under("outside") }; break;
            case "unsupported-script":
                File.Delete(Path.Combine(bad.Directory, "scripts", "run.py"));
                File.WriteAllText(Path.Combine(bad.Directory, "scripts", "run.sh"), "unsupported interpreter");
                break;
            case "manifest-budget":
                for (var index = 0; index < 64; index++)
                    File.WriteAllText(Path.Combine(bad.Directory, "scripts", $"extra-{index}.py"), "print('extra')");
                break;
        }
        foreach (var list in new IReadOnlyList<AiSkill>[] { new[] { bad, good }, new[] { good, bad } })
        {
            using var bridge = await fixture.Execution.PrepareAsync(list, fixture.Run, CancellationToken.None);
            Assert.IsNotNull(bridge);
            CollectionAssert.AreEqual(new[] { "good" }, bridge.SkillIds.ToArray());
            var registry = await SkillExecutionRegistry.LoadAsync(bridge.Arguments[1], CancellationToken.None);
            Assert.AreEqual("good", registry.Skills.Single().SkillId);
            Assert.AreEqual("print('good')", File.ReadAllText(registry.Skills.Single().ScriptPath));
        }
    }

    [TestMethod]
    public async Task InstructionOnlyPackageWithADottedIdAndLargeAssetsDoesNotPrepareExecutionRuntimes()
    {
        using var fixture = new Fixture();
        var guide = fixture.Install("guide.v1", new() { ["references/guide.md"] = "Instructions only." });
        File.WriteAllBytes(Path.Combine(guide.Directory, "references", "large.dat"), new byte[2 * 1024 * 1024]);

        var bridge = await fixture.Execution.PrepareAsync([guide], fixture.Run, CancellationToken.None);

        Assert.IsNull(bridge);
        Assert.AreEqual(0, fixture.Runtime.EnsureCalls);
        Assert.IsFalse(Directory.Exists(fixture.Paths.Under("skill-execution")));
    }

    [TestMethod]
    public async Task DottedSkillIdsUseDistinctStableToolSafeNames()
    {
        using var fixture = new Fixture();
        var dotted = fixture.Install("guide.v1", new() { ["scripts/run.py"] = "print('dotted')" });
        var plain = fixture.Install("guide-v1", new() { ["scripts/run.py"] = "print('plain')" });
        using var first = await fixture.Execution.PrepareAsync([dotted, plain], fixture.Run, CancellationToken.None);
        using var second = await fixture.Execution.PrepareAsync([dotted, plain], fixture.Run, CancellationToken.None);

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.HasCount(2, first.SkillIds);
        Assert.HasCount(2, first.SkillIds.Distinct(StringComparer.OrdinalIgnoreCase));
        foreach (var id in first.SkillIds)
        {
            Assert.IsLessThanOrEqualTo(64, id.Length);
            Assert.IsTrue(id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));
        }
        Assert.Contains("guide-v1", first.SkillIds);
        CollectionAssert.AreEqual(first.SkillIds.ToArray(), second.SkillIds.ToArray());
        Assert.HasCount(2, (await SkillExecutionRegistry.LoadAsync(first.Arguments[1], CancellationToken.None)).Skills);
    }

    [TestMethod]
    public async Task EmptyPackageInitializersRemainDependenciesWithoutBecomingExecutableTools()
    {
        using var fixture = new Fixture();
        var skill = fixture.Install("python-package", new()
        {
            ["scripts/run.py"] = "print('entry')",
            ["scripts/__init__.py"] = "",
            ["scripts/helper/__init__.py"] = ""
        });

        using var bridge = await fixture.Execution.PrepareAsync([skill], fixture.Run, CancellationToken.None);

        Assert.IsNotNull(bridge);
        CollectionAssert.AreEqual(new[] { "python-package" }, bridge.SkillIds.ToArray());
        var registry = await SkillExecutionRegistry.LoadAsync(bridge.Arguments[1], CancellationToken.None);
        Assert.AreEqual("run.py", Path.GetFileName(registry.Skills.Single().ScriptPath));
        var initializers = registry.Skills.Single().PackageFiles!.Where(x => Path.GetFileName(x.Path) == "__init__.py").ToArray();
        Assert.HasCount(2, initializers);
        Assert.IsTrue(initializers.All(x => File.Exists(x.Path) && new FileInfo(x.Path).Length == 0));
    }

    [TestMethod]
    public async Task AggregateManifestBudgetSkipsAnOverBudgetPackageAndKeepsALaterSmallPackage()
    {
        using var fixture = new Fixture();
        var skills = new List<AiSkill>();
        for (var package = 0; package < 4; package++)
        {
            var files = new Dictionary<string, string>();
            for (var script = 0; script < 32; script++) files[$"scripts/run-{script}.py"] = "print('fixture')";
            skills.Add(fixture.Install("many-" + package, files));
        }
        skills.Add(fixture.Install("later-small", new() { ["scripts/run.py"] = "print('small')" }));

        using var bridge = await fixture.Execution.PrepareAsync(skills, fixture.Run, CancellationToken.None);

        Assert.IsNotNull(bridge);
        Assert.HasCount(97, bridge.SkillIds);
        Assert.Contains("later-small", bridge.SkillIds);
        var registry = await SkillExecutionRegistry.LoadAsync(bridge.Arguments[1], CancellationToken.None);
        Assert.IsLessThanOrEqualTo(4096, registry.Skills.Sum(x => x.PackageFiles!.Count));
        Assert.IsFalse(registry.Skills.Any(x => x.Description!.StartsWith("Installed skill many-3,", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task DependencyDirectoryTraversalBudgetRejectsOnlyTheOversizedPackage(bool badFirst)
    {
        using var fixture = new Fixture();
        var good = fixture.Install("good", new() { ["scripts/run.py"] = "print('good')" });
        var dependencies = new Dictionary<string, string> { ["scripts/run.py"] = "print('bad')" };
        // Wide, shallow branches stay below Windows path limits while exceeding 1,024
        // directories with only 66 files. Entry discovery intentionally skips node_modules.
        for (var branch = 0; branch < 64; branch++)
        {
            var relative = "node_modules/b" + branch.ToString("x2") + "/" +
                string.Join('/', Enumerable.Repeat("d", 16)) + "/data.txt";
            dependencies[relative] = "Read-only dependency data.";
        }
        var bad = fixture.Install("many-directories", dependencies);
        var sourceEntryCount = Directory.EnumerateFileSystemEntries(bad.Directory, "*", SearchOption.AllDirectories).Count();
        Assert.IsGreaterThan(1024, Directory.EnumerateDirectories(bad.Directory, "*", SearchOption.AllDirectories).Count());
        Assert.IsLessThanOrEqualTo(256, Directory.EnumerateFiles(bad.Directory, "*", SearchOption.AllDirectories).Count());

        using var bridge = await fixture.Execution.PrepareAsync(badFirst ? [bad, good] : [good, bad], fixture.Run, CancellationToken.None);

        Assert.IsNotNull(bridge);
        CollectionAssert.AreEqual(new[] { "good" }, bridge.SkillIds.ToArray());
        var registry = await SkillExecutionRegistry.LoadAsync(bridge.Arguments[1], CancellationToken.None);
        Assert.AreEqual("good", registry.Skills.Single().SkillId);
        Assert.AreEqual("print('good')", File.ReadAllText(registry.Skills.Single().ScriptPath));
        Assert.AreEqual(1, fixture.Runtime.EnsureCalls);
        Assert.HasCount(sourceEntryCount, Directory.EnumerateFileSystemEntries(bad.Directory, "*", SearchOption.AllDirectories));
    }

    [TestMethod]
    public async Task AReparsePointInOneExecutablePackageDoesNotDisableAnotherPackage()
    {
        using var fixture = new Fixture();
        var good = fixture.Install("good", new() { ["scripts/run.py"] = "print('good')" });
        var bad = fixture.Install("bad", new() { ["scripts/run.py"] = "print('bad')" });
        var destination = fixture.Paths.Under("reparse-target");
        Directory.CreateDirectory(destination);
        var link = Path.Combine(bad.Directory, "linked-reference");
        try { Directory.CreateSymbolicLink(link, destination); }
        catch (Exception error) when (error is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
        { Assert.Inconclusive("The test host cannot create the synthetic reparse point."); }
        try
        {
            using var bridge = await fixture.Execution.PrepareAsync([bad, good], fixture.Run, CancellationToken.None);
            Assert.IsNotNull(bridge);
            CollectionAssert.AreEqual(new[] { "good" }, bridge.SkillIds.ToArray());
            Assert.IsTrue(Directory.Exists(destination));
        }
        finally { if (Directory.Exists(link)) Directory.Delete(link); }
    }

    [TestMethod]
    public async Task AnUntrustedOutsideDirectoryExposesNoToolWhenNoUsablePackageRemains()
    {
        using var fixture = new Fixture();
        var bad = fixture.Install("bad", new() { ["scripts/run.py"] = "print('bad')" }) with { Directory = fixture.Paths.Under("outside") };

        var bridge = await fixture.Execution.PrepareAsync([bad], fixture.Run, CancellationToken.None);

        Assert.IsNull(bridge);
        Assert.AreEqual(0, fixture.Runtime.EnsureCalls);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly InstallFixture _owner = new();
        internal ManagedAiPaths Paths => _owner.Paths;
        internal Runtime Runtime { get; }
        internal string Run { get; }
        internal WindowsManagedAiSkillExecution Execution { get; }
        internal Fixture()
        {
            Runtime = new(Paths);
            Run = Paths.Under("runs", "bad-skill-test");
            Directory.CreateDirectory(Run);
            var host = Paths.Under("TableCloth.exe");
            File.WriteAllBytes(host, [0x54, 0x43, 0x48, 0]);
            Execution = new(Paths, Runtime, host);
        }
        internal AiSkill Install(string id, Dictionary<string, string> files)
        {
            var directory = Paths.Under("profiles", "openai-codex", "codex-home", "skills", id);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "SKILL.md"), "Synthetic test skill.", new UTF8Encoding(false));
            foreach (var (relative, text) in files)
            {
                var destination = Path.Combine(directory, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.WriteAllText(destination, text, new UTF8Encoding(false));
            }
            return new(id, id, "Fixture only.", directory, true);
        }
        public void Dispose() => _owner.Dispose();
    }

    private sealed class Runtime(ManagedAiPaths paths) : IManagedAiScriptExecutionRuntime
    {
        internal int EnsureCalls;
        public string PythonExecutable => paths.Under("skill-execution", "runtimes", "python", "python.exe");
        public string NodeExecutable => paths.Under("skill-execution", "runtimes", "node", "node.exe");
        public Task<AiScriptRuntimeStatus> GetStatusAsync(CancellationToken token) => Task.FromResult(Status());
        public Task<AiScriptRuntimeStatus> EnsureInstalledAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            EnsureCalls++;
            foreach (var file in new[] { PythonExecutable, NodeExecutable })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllBytes(file, [0x54, 0x43, 0x46, 0]);
            }
            return Task.FromResult(Status());
        }
        public Task<IReadOnlyList<ScriptRuntimeFile>> GetVerifiedFilesAsync(CancellationToken token)
            => Task.FromResult<IReadOnlyList<ScriptRuntimeFile>>([new(PythonExecutable, Hash(PythonExecutable)), new(NodeExecutable, Hash(NodeExecutable))]);
        private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        private static AiScriptRuntimeStatus Status() => new(new("Python", "fixture", "fixture", true, null),
            new("Node.js", "fixture", "fixture", true, null), new(null, null, null));
    }
}
