using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TableCloth.AppContainerPoc;
using TableCloth.ManagedAi.Windows;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class WindowsSkillRegistrationTests
{
    [TestMethod]
    public async Task BokWithoutCalculatorRegistersItsActualInstalledPythonEntryPoint()
    {
        using var fixture = new RegistrationFixture();
        var bok = fixture.InstallUpstream("bok-ecos-stats");
        using var bridge = await fixture.Execution().PrepareAsync([bok], fixture.Run, CancellationToken.None);

        Assert.IsNotNull(bridge);
        CollectionAssert.AreEqual(new[] { "bok-ecos-stats" }, bridge.SkillIds.ToArray());
        var registry = await SkillExecutionRegistry.LoadAsync(bridge.Arguments[1], CancellationToken.None);
        var registered = registry.Skills.Single();
        Assert.AreEqual("python-script", registered.Kind);
        Assert.AreEqual("bok_ecos.py", Path.GetFileName(registered.ScriptPath));
        Assert.AreEqual(RegistrationFixture.Hash(Path.Combine(bok.Directory, "scripts", "bok_ecos.py")), registered.Sha256);
        Assert.IsFalse(registered.RequiresNetwork, "An unprovenanced local package cannot declare a network route by its ID.");
        Assert.AreEqual(1, fixture.Runtime.EnsureCalls);
    }

    [TestMethod]
    [DataRow("user-custom-python-skill", "scripts/run.py", "python-script")]
    [DataRow("user-custom-node-skill", "scripts/run.js", "node-script")]
    [DataRow("user-custom-node-cjs", "scripts/run.cjs", "node-script")]
    [DataRow("user-custom-node-esm", "scripts/run.mjs", "node-script")]
    public async Task InstalledScriptRegistrationDoesNotDependOnAProductSkillIdAllowlist(string id, string entryPoint, string kind)
    {
        using var fixture = new RegistrationFixture();
        var skill = fixture.Install(id, new() { [entryPoint] = "synthetic script bytes; never executed" });
        using var bridge = await fixture.Execution().PrepareAsync([skill], fixture.Run, CancellationToken.None);

        Assert.IsNotNull(bridge);
        var registered = (await SkillExecutionRegistry.LoadAsync(bridge.Arguments[1], CancellationToken.None)).Skills.Single();
        Assert.AreEqual(id, registered.SkillId);
        Assert.AreEqual(kind, registered.Kind);
        Assert.AreEqual(RegistrationFixture.Hash(Path.Combine(skill.Directory, entryPoint)), registered.Sha256);
        Assert.IsFalse(registered.RequiresNetwork);
    }

    [TestMethod]
    public async Task MultipleEntryPointsHaveDistinctStableNamesAndKeepTheirInstalledPackageDependencies()
    {
        using var fixture = new RegistrationFixture();
        var skill = fixture.Install("multi-entry-skill", new()
        {
            ["scripts/first.py"] = "print('first')",
            ["scripts/second.cjs"] = "console.log('second')",
            ["references/input-format.md"] = "An installed reference.",
            ["tests/test_first.py"] = "This test module is not an executable tool.",
            ["node_modules/local/index.js"] = "module.exports = 1;"
        });
        using var first = await fixture.Execution().PrepareAsync([skill], fixture.Run, CancellationToken.None);
        using var second = await fixture.Execution().PrepareAsync([skill], fixture.Run, CancellationToken.None);

        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        Assert.HasCount(2, first.SkillIds);
        Assert.AreEqual(2, first.SkillIds.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        CollectionAssert.AreEqual(first.SkillIds.ToArray(), second.SkillIds.ToArray());
        var registered = (await SkillExecutionRegistry.LoadAsync(first.Arguments[1], CancellationToken.None)).Skills;
        CollectionAssert.AreEquivalent(new[] { "first.py", "second.cjs" }, registered.Select(x => Path.GetFileName(x.ScriptPath)).ToArray());
        Assert.AreEqual(1, registered.Select(x => x.PackageRoot).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.IsTrue(registered.All(x => x.PackageFiles!.Any(file => Path.GetFileName(file.Path) == "input-format.md")));
        Assert.IsTrue(registered.All(x => x.PackageFiles!.Any(file => file.Path.EndsWith(Path.Combine("node_modules", "local", "index.js"), StringComparison.OrdinalIgnoreCase))));
    }

    [TestMethod]
    public async Task ChangedInstalledScriptGetsAFreshHashAndIndependentPerInvocationSnapshot()
    {
        using var fixture = new RegistrationFixture();
        var skill = fixture.Install("updated-python-skill", new() { ["scripts/run.py"] = "print('version one')" });
        var execution = fixture.Execution();
        var first = await execution.PrepareAsync([skill], fixture.Run, CancellationToken.None);
        Assert.IsNotNull(first);
        var firstRegistration = (await SkillExecutionRegistry.LoadAsync(first.Arguments[1], CancellationToken.None)).Skills.Single();
        var oldHash = firstRegistration.Sha256;
        File.WriteAllText(Path.Combine(skill.Directory, "scripts", "run.py"), "print('version two')", new UTF8Encoding(false));

        using var second = await execution.PrepareAsync([skill], fixture.Run, CancellationToken.None);
        Assert.IsNotNull(second);
        var secondRegistration = (await SkillExecutionRegistry.LoadAsync(second.Arguments[1], CancellationToken.None)).Skills.Single();
        Assert.AreNotEqual(first.SnapshotDirectory, second.SnapshotDirectory);
        Assert.AreNotEqual(oldHash, secondRegistration.Sha256);
        Assert.AreEqual(oldHash, RegistrationFixture.Hash(firstRegistration.ScriptPath));
        Assert.AreEqual(RegistrationFixture.Hash(Path.Combine(skill.Directory, "scripts", "run.py")), secondRegistration.Sha256);

        var firstSnapshot = first.SnapshotDirectory;
        var firstManifest = first.Arguments[1];
        first.Dispose();
        first.Dispose();
        Assert.IsFalse(File.Exists(firstManifest));
        Assert.IsFalse(Directory.Exists(firstSnapshot));
        Assert.IsTrue(File.Exists(second.Arguments[1]));
        Assert.IsTrue(Directory.Exists(second.SnapshotDirectory));
        Assert.AreEqual("print('version two')", File.ReadAllText(Path.Combine(skill.Directory, "scripts", "run.py")));
    }

    [TestMethod]
    public async Task SnapshotIntegrityRejectsChangesBeforeAnExecutorCanRun()
    {
        using var fixture = new RegistrationFixture();
        var skill = fixture.Install("integrity-skill", new() { ["scripts/run.py"] = "print(1)" });
        using var bridge = await fixture.Execution().PrepareAsync([skill], fixture.Run, CancellationToken.None);
        Assert.IsNotNull(bridge);
        var registry = await SkillExecutionRegistry.LoadAsync(bridge.Arguments[1], CancellationToken.None);
        File.AppendAllText(registry.Skills.Single().ScriptPath, "\nprint(2)");
        var executed = false;

        await Assert.ThrowsExactlyAsync<BrokerValidationException>(() => SkillExecutionRegistry.LoadAsync(bridge.Arguments[1],
            (_, _) => { executed = true; return Task.FromResult(new SandboxRunResult(0, "", "", false, false, true)); }, CancellationToken.None));

        Assert.IsFalse(executed);
    }

    [TestMethod]
    public async Task SecretAndEnvironmentFilesDoNotEnterTheReadOnlyPackageSnapshot()
    {
        using var fixture = new RegistrationFixture();
        var skill = fixture.Install("package-filter-skill", new()
        {
            ["scripts/run.py"] = "print('safe')",
            ["references/readme.md"] = "public package reference",
            ["LICENSE"] = "",
            [".env"] = "SYNTHETIC_PRIVATE_VALUE=not-a-real-key",
            ["credentials.env"] = "SYNTHETIC_PRIVATE_VALUE=not-a-real-key",
            ["secrets.env"] = "SYNTHETIC_PRIVATE_VALUE=not-a-real-key",
            ["nested/secrets.env"] = "SYNTHETIC_PRIVATE_VALUE=not-a-real-key",
            ["private.key"] = "synthetic key bytes",
            ["certificate.pem"] = "synthetic cert bytes",
            ["certificate.pfx"] = "synthetic cert bytes",
            ["certificate.p12"] = "synthetic cert bytes",
            [".hidden/private.py"] = "hidden script",
            [".venv/main.py"] = "virtual environment script",
            ["__pycache__/run.py"] = "cached script"
        });
        using var bridge = await fixture.Execution().PrepareAsync([skill], fixture.Run, CancellationToken.None);
        Assert.IsNotNull(bridge);
        var registration = (await SkillExecutionRegistry.LoadAsync(bridge.Arguments[1], CancellationToken.None)).Skills.Single();
        var names = registration.PackageFiles!.Select(file => Path.GetRelativePath(registration.PackageRoot!, file.Path).Replace('\\', '/')).ToArray();

        CollectionAssert.AreEquivalent(new[] { "SKILL.md", "scripts/run.py", "references/readme.md", "LICENSE" }, names);
        Assert.IsTrue(File.Exists(Path.Combine(skill.Directory, "secrets.env")), "Excluding a file from the worker must not delete the installed original.");
    }

    [TestMethod]
    [DataRow("disabled")]
    [DataRow("invalid")]
    [DataRow("no-script")]
    [DataRow("removed-script")]
    public async Task NonExecutableOrIneligiblePackagesNeverPrepareDedicatedRuntimes(string condition)
    {
        using var fixture = new RegistrationFixture();
        var files = condition == "no-script" ? new Dictionary<string, string> { ["references/guide.md"] = "Instructions only." }
            : new Dictionary<string, string> { ["scripts/run.py"] = "print(1)" };
        var skill = fixture.Install("not-executable", files) with { Enabled = condition != "disabled", IsValid = condition != "invalid" };
        if (condition == "removed-script") File.Delete(Path.Combine(skill.Directory, "scripts", "run.py"));

        var bridge = await fixture.Execution().PrepareAsync([skill], fixture.Run, CancellationToken.None);

        Assert.IsNull(bridge);
        Assert.AreEqual(0, fixture.Runtime.EnsureCalls);
        Assert.IsFalse(Directory.Exists(fixture.Paths.Under("skill-execution")));
    }

    [TestMethod]
    public async Task AnInstalledSkillCannotPointOutsideItsExpectedAppOwnedDirectory()
    {
        using var fixture = new RegistrationFixture();
        var skill = fixture.Install("source-boundary", new() { ["scripts/run.py"] = "print(1)" });
        var redirected = skill with { Directory = fixture.Paths.Under("other-source") };
        Directory.CreateDirectory(redirected.Directory);
        File.WriteAllText(Path.Combine(redirected.Directory, "run.py"), "print('outside')");

        using var bridge = await fixture.Execution().PrepareAsync([redirected], fixture.Run, CancellationToken.None);

        Assert.IsNull(bridge);
        Assert.AreEqual(0, fixture.Runtime.EnsureCalls);
    }

    [TestMethod]
    [DataRow("id", "kosis-stats")]
    [DataRow("ref", "2222222222222222222222222222222222222222")]
    [DataRow("repository", "https://github.com/another/skill")]
    [DataRow("path", "another-path")]
    [DataRow("network", "none")]
    public async Task CatalogNetworkDeclarationsRequireMatchingInstalledProvenance(string field, string replacement)
    {
        using var fixture = new RegistrationFixture();
        var skill = fixture.InstallUpstream("bok-ecos-stats") with { Source = RegistrationFixture.BokSource };
        var entry = RegistrationFixture.BokEntry;
        entry = field switch
        {
            "id" => entry with { Id = replacement },
            "ref" => entry with { SourceRef = replacement },
            "repository" => entry with { Repository = replacement },
            "path" => entry with { SourcePath = replacement },
            "network" => entry with { Network = replacement },
            _ => throw new InvalidOperationException()
        };
        using var bridge = await fixture.Execution(new Marketplace(entry)).PrepareAsync([skill], fixture.Run, CancellationToken.None);
        Assert.IsNotNull(bridge);
        var registration = (await SkillExecutionRegistry.LoadAsync(bridge.Arguments[1], CancellationToken.None)).Skills.Single();

        Assert.IsFalse(registration.RequiresNetwork);
        Assert.IsNotNull(registration.NetworkHosts);
        Assert.IsEmpty(registration.NetworkHosts);
    }

    [TestMethod]
    public async Task MatchingCatalogPinRegistersOnlyTheBoundedPublicNetworkHosts()
    {
        using var fixture = new RegistrationFixture();
        var skill = fixture.InstallUpstream("bok-ecos-stats") with { Source = RegistrationFixture.BokSource };
        var marketplace = new Marketplace(RegistrationFixture.BokEntry);
        using var bridge = await fixture.Execution(marketplace).PrepareAsync([skill], fixture.Run, CancellationToken.None);
        Assert.IsNotNull(bridge);
        var registration = (await SkillExecutionRegistry.LoadAsync(bridge.Arguments[1], CancellationToken.None)).Skills.Single();

        Assert.IsTrue(registration.RequiresNetwork);
        CollectionAssert.AreEquivalent(new[] { "ecos.bok.or.kr", "k-skill-proxy.nomadamas.org" }, registration.NetworkHosts!.ToArray());
        Assert.AreEqual(1, marketplace.ListCalls);
        Assert.AreEqual(0, marketplace.MutationCalls);
    }

    [TestMethod]
    [DataRow("percent.part")]
    [DataRow("percent.share")]
    [DataRow("percent.change")]
    [DataRow("percent.adjust")]
    [DataRow("percent.points")]
    [DataRow("discount.single")]
    [DataRow("discount.sequential")]
    [DataRow("discount.reverse")]
    [DataRow("unit_price")]
    [DataRow("bill_split")]
    [DataRow("interest.simple")]
    [DataRow("interest.compound")]
    [DataRow("savings.recurring")]
    [DataRow("savings.target")]
    [DataRow("loan")]
    [DataRow("date.diff")]
    [DataRow("date.add")]
    [DataRow("date.age")]
    [DataRow("date.weekday")]
    [DataRow("time.convert")]
    [DataRow("unit.convert")]
    [DataRow("bmi")]
    public async Task GenericCalculatorTransportPassesAll22OperationNamesToTheContainedWorkerWithoutASixOperationFilter(string operation)
    {
        using var fixture = new RegistrationFixture();
        var skill = fixture.InstallUpstream("tablecloth-calculators");
        using var bridge = await fixture.Execution().PrepareAsync([skill], fixture.Run, CancellationToken.None);
        Assert.IsNotNull(bridge);
        var requests = new List<SandboxRunRequest>();
        var registry = await SkillExecutionRegistry.LoadAsync(bridge.Arguments[1], (request, _) =>
        {
            requests.Add(request);
            return Task.FromResult(new SandboxRunResult(0, "fixture worker result", "", false, false, true));
        }, CancellationToken.None);
        // This verifies the transport boundary only. The pinned upstream test suite separately
        // verifies required fields and mathematical outputs; no fake executable is launched here.
        var stdin = JsonSerializer.Serialize(new { operation });
        var input = JsonSerializer.SerializeToElement(new { arguments = Array.Empty<string>(), stdin });

        var result = await registry.ExecuteAsync(registry.Skills.Single().SkillId, input, CancellationToken.None);

        Assert.AreEqual("completed", result.Status);
        Assert.IsTrue(result.WasAppContainer);
        Assert.HasCount(1, requests);
        Assert.AreEqual(stdin, requests[0].Stdin);
        Assert.IsFalse(requests[0].AllowNetwork);
        Assert.Contains(registry.Skills.Single().ScriptPath, requests[0].Arguments);
        Assert.Contains(registry.Skills.Single().PackageRoot!, requests[0].ReadOnlyDirectories);
        Assert.AreEqual(fixture.Runtime.PythonExecutable, requests[0].Executable);
    }

    [TestMethod]
    public async Task GenericNodeArgumentsAndStdinUseTheInjectedVerifiedRuntimeWithoutLaunchingIt()
    {
        using var fixture = new RegistrationFixture();
        var skill = fixture.Install("node-argument-skill", new() { ["scripts/run.cjs"] = "console.log('fixture')" });
        using var bridge = await fixture.Execution().PrepareAsync([skill], fixture.Run, CancellationToken.None);
        Assert.IsNotNull(bridge);
        SandboxRunRequest? captured = null;
        var registry = await SkillExecutionRegistry.LoadAsync(bridge.Arguments[1], (request, _) =>
        {
            captured = request;
            return Task.FromResult(new SandboxRunResult(0, "{\"sum\":42}", "", false, false, true));
        }, CancellationToken.None);
        var result = await registry.ExecuteAsync(registry.Skills.Single().SkillId,
            JsonSerializer.SerializeToElement(new { arguments = new[] { "--mode", "sum" }, stdin = "{\"a\":12,\"b\":30}" }), CancellationToken.None);

        Assert.AreEqual("completed", result.Status);
        Assert.IsNotNull(captured);
        Assert.AreEqual(fixture.Runtime.NodeExecutable, captured.Executable);
        CollectionAssert.AreEqual(new[] { "--preserve-symlinks-main", "--preserve-symlinks", registry.Skills.Single().ScriptPath, "--mode", "sum" }, captured.Arguments.ToArray());
        Assert.AreEqual("{\"a\":12,\"b\":30}", captured.Stdin);
        Assert.IsFalse(captured.AllowNetwork);
        Assert.AreEqual(1, captured.MaxProcesses);
    }

    private sealed class Marketplace(SkillCatalogEntry entry) : IManagedAiSkillMarketplace
    {
        internal int ListCalls;
        internal int MutationCalls;
        public Task<IReadOnlyList<SkillCatalogEntry>> ListAsync(CancellationToken cancellationToken)
        { ListCalls++; return Task.FromResult<IReadOnlyList<SkillCatalogEntry>>([entry]); }
        public Task<IReadOnlyList<AiSkill>> InstallAsync(string id, CancellationToken cancellationToken)
        { MutationCalls++; throw new InvalidOperationException("Registration must not install a skill."); }
        public Task<IReadOnlyList<AiSkill>> UpdateAsync(string id, CancellationToken cancellationToken)
        { MutationCalls++; throw new InvalidOperationException("Registration must not update a skill."); }
    }

    private sealed class RegistrationFixture : IDisposable
    {
        private readonly InstallFixture _owner = new();
        internal ManagedAiPaths Paths => _owner.Paths;
        internal FakeScriptRuntime Runtime { get; }
        internal string Run { get; }
        internal string Host { get; }
        internal const string SourcePin = "0102e5133865f00886e37ac8cf39a49e2c7a3d39";
        internal static AiSkillSource BokSource => new("bok-ecos-stats", "https://github.com/NomaDamas/k-skill", "bok-ecos-stats", SourcePin);
        internal static SkillCatalogEntry BokEntry => new("bok-ecos-stats", "Economics", "BOK", "Public BOK data.",
            "https://github.com/NomaDamas/k-skill", "bok-ecos-stats", SourcePin, "NomaDamas", "MIT", "upstream", "required", "Public sample key.");

        internal RegistrationFixture()
        {
            Runtime = new(Paths);
            Run = Paths.Under("runs", "registration-test");
            Directory.CreateDirectory(Run);
            Host = Paths.Under("TableCloth.exe");
            File.WriteAllBytes(Host, [0x54, 0x43, 0x48, 0x00]);
        }
        internal WindowsManagedAiSkillExecution Execution(IManagedAiSkillMarketplace? marketplace = null)
            => new(Paths, Runtime, Host, marketplace);
        internal AiSkill Install(string id, Dictionary<string, string> files)
        {
            var directory = Paths.Under("profiles", "openai-codex", "codex-home", "skills", id);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "SKILL.md"), "---\nname: " + id + "\ndescription: Synthetic registration fixture.\n---\n", new UTF8Encoding(false));
            foreach (var (relative, contents) in files)
            {
                var destination = Path.Combine(directory, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.WriteAllText(destination, contents, new UTF8Encoding(false));
            }
            return new(id, id, "Synthetic registration fixture.", directory, true);
        }
        internal AiSkill InstallUpstream(string id)
        {
            var repository = new DirectoryInfo(AppContext.BaseDirectory);
            while (repository is not null && !Directory.Exists(Path.Combine(repository.FullName, "tools", "appcontainer-skill-poc", "skills", id))) repository = repository.Parent;
            Assert.IsNotNull(repository, "The pinned upstream fixture must be present in the checkout.");
            var source = Path.Combine(repository.FullName, "tools", "appcontainer-skill-poc", "skills", id);
            var skill = Install(id, new());
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(skill.Directory, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination, overwrite: true);
            }
            return skill;
        }
        internal static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        public void Dispose() => _owner.Dispose();
    }

    private sealed class FakeScriptRuntime(ManagedAiPaths paths) : IManagedAiScriptExecutionRuntime
    {
        internal int EnsureCalls;
        public string PythonExecutable => paths.Under("skill-execution", "runtimes", "python", "python.exe");
        public string NodeExecutable => paths.Under("skill-execution", "runtimes", "node", "node.exe");
        public Task<AiScriptRuntimeStatus> GetStatusAsync(CancellationToken token) => Task.FromResult(Status());
        public Task<AiScriptRuntimeStatus> EnsureInstalledAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            EnsureCalls++;
            foreach (var executable in new[] { PythonExecutable, NodeExecutable })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
                File.WriteAllBytes(executable, [0x54, 0x43, 0x46, 0x00]);
            }
            return Task.FromResult(Status());
        }
        public Task<IReadOnlyList<ScriptRuntimeFile>> GetVerifiedFilesAsync(CancellationToken token)
            => Task.FromResult<IReadOnlyList<ScriptRuntimeFile>>([new(PythonExecutable, RegistrationFixture.Hash(PythonExecutable)), new(NodeExecutable, RegistrationFixture.Hash(NodeExecutable))]);
        private static AiScriptRuntimeStatus Status()
            => new(new("Python", "fixture", "fixture", true, null), new("Node.js", "fixture", "fixture", true, null), new(null, null, null));
    }
}
