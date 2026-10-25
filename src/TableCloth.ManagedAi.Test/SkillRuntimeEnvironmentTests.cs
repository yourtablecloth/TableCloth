using System.Diagnostics;
using TableCloth.ManagedAi.OpenAi;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class SkillRuntimeEnvironmentTests
{
    [TestMethod]
    public void ChildUsesOnlyDedicatedToolsAndDoesNotInheritHostPackageConfiguration()
    {
        using var fixture = new ToolEnvironmentFixture();
        var hostPath = Environment.GetEnvironmentVariable("PATH");
        var process = new ProcessStartInfo();
        process.Environment.Clear();
        foreach (var key in new[] { "PATH", "NODE_PATH", "NODE_OPTIONS", "PYTHONPATH", "PYTHONHOME", "VIRTUAL_ENV",
            "NPM_TOKEN", "NPM_CONFIG_REGISTRY", "API_K_DART", "OPENAI_API_KEY", "HOMEDRIVE", "HOMEPATH" })
            process.Environment[key] = "host-fixture";
        process.Environment["CODEX_HOME"] = fixture.Paths.Profile;
        SkillRuntimeProcessEnvironment.Apply(process, fixture.Paths, fixture.Environment);

        var environment = process.Environment;
        var search = environment["PATH"]!.Split(Path.PathSeparator);
        Assert.AreEqual(Path.Combine(fixture.Paths.SkillToolData, "bin"), search[0]);
        Assert.AreEqual(fixture.Environment.NodeJs.RootDirectory, search[1]);
        Assert.AreEqual(fixture.Environment.Python.RootDirectory, search[2]);
        Assert.DoesNotContain("host-fixture", environment.Values);
        foreach (var key in new[] { "NODE_PATH", "NODE_OPTIONS", "PYTHONPATH", "PYTHONHOME", "VIRTUAL_ENV",
            "NPM_TOKEN", "NPM_CONFIG_REGISTRY", "API_K_DART", "OPENAI_API_KEY" })
            Assert.IsFalse(environment.ContainsKey(key), key);
        foreach (var key in new[] { "HOME", "USERPROFILE", "APPDATA", "LOCALAPPDATA", "TEMP", "TMP", "NPM_CONFIG_CACHE",
            "NPM_CONFIG_PREFIX", "NPM_CONFIG_USERCONFIG", "NPM_CONFIG_GLOBALCONFIG", "PYTHONUSERBASE" })
            Assert.StartsWith(fixture.Paths.SkillToolData + Path.DirectorySeparatorChar, environment[key]!);
        Assert.AreNotEqual(environment["NPM_CONFIG_USERCONFIG"], environment["NPM_CONFIG_GLOBALCONFIG"]);
        Assert.AreEqual(environment["USERPROFILE"], environment["HOMEDRIVE"] + environment["HOMEPATH"]);
        Assert.AreEqual(fixture.Environment.Python.EntryPoint, environment["KSKILL_PYTHON"]);
        Assert.AreEqual(fixture.Paths.Profile, environment["CODEX_HOME"]);
        Assert.AreEqual("1", environment["PYTHONNOUSERSITE"]);
        Assert.AreEqual(".COM;.EXE;.BAT;.CMD", environment["PATHEXT"]);
        Assert.AreEqual(hostPath, Environment.GetEnvironmentVariable("PATH"));
        foreach (var command in new[] { "npm", "npx" })
        {
            var shim = File.ReadAllText(Path.Combine(search[0], command + ".cmd"));
            Assert.Contains("%~dp0", shim);
            Assert.Contains("DisableDelayedExpansion", shim);
            Assert.DoesNotContain(fixture.Paths.Root, shim);
            Assert.Contains(command + "-cli.js", shim);
        }
    }

    [TestMethod]
    public void RefusesToolsOutsideItsOwnedReleaseTree()
    {
        using var fixture = new ToolEnvironmentFixture();
        var outside = Path.Combine(fixture.Paths.Root, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "node.exe"), "fixture");
        var untrusted = fixture.Environment with { NodeJs = new("24.22.1", outside, Path.Combine(outside, "node.exe")) };
        Assert.AreEqual(AiFailureCode.InvalidPath, Assert.ThrowsExactly<ManagedAiException>(() =>
            SkillRuntimeProcessEnvironment.Apply(new(), fixture.Paths, untrusted)).Code);
        Assert.IsFalse(Directory.Exists(fixture.Paths.SkillToolData));
    }

    [TestMethod]
    public async Task CodexPreparesCompanionsBeforeTakingItsMutationLease()
    {
        var companion = new CompanionManager();
        using var fixture = new InstallFixture(companion);
        companion.Paths = fixture.Paths;
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        Assert.AreEqual(1, companion.InstallCalls);
        Assert.IsTrue(companion.CouldAcquireLease);
        Assert.IsNotNull(await fixture.Installer.GetActiveAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task CompanionFailurePreservesActiveCodexWithoutDownloadingItsUpdate()
    {
        var companion = new CompanionManager();
        using var fixture = new InstallFixture(companion);
        companion.Paths = fixture.Paths;
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var before = File.ReadAllText(fixture.Paths.Current);
        var downloads = fixture.PackageDownloads;
        fixture.Version = "1.2.4";
        companion.Fail = true;
        await Assert.ThrowsExactlyAsync<ManagedAiException>(() => fixture.Installer.InstallAsync(null, null, CancellationToken.None));
        Assert.AreEqual(before, File.ReadAllText(fixture.Paths.Current));
        Assert.AreEqual(downloads, fixture.PackageDownloads);
    }

    private sealed class CompanionManager : IManagedAiSkillRuntimeManager
    {
        public ManagedAiPaths Paths = null!;
        public int InstallCalls;
        public bool CouldAcquireLease;
        public bool Fail;
        public Task<SkillRuntimeEnvironment?> GetActiveAsync(CancellationToken cancellationToken) => Task.FromResult<SkillRuntimeEnvironment?>(null);
        public Task<SkillRuntimeEnvironment> InstallAsync(IProgress<AiProgress>? progress, CancellationToken cancellationToken)
        {
            InstallCalls++;
            using var lease = Paths.AcquireOperation();
            CouldAcquireLease = true;
            if (Fail) throw new ManagedAiException(AiFailureCode.IntegrityMismatch);
            return Task.FromResult<SkillRuntimeEnvironment>(null!);
        }
    }
}

internal sealed class ToolEnvironmentFixture : IDisposable
{
    public ManagedAiPaths Paths { get; } = new(Path.Combine(Path.GetTempPath(), "tc-tools-한글-%fixture%-" + Guid.NewGuid().ToString("N")));
    public SkillRuntimeEnvironment Environment { get; }
    public ToolEnvironmentFixture()
    {
        var release = Paths.Under("runtimes", "skill-tools", "releases", "fixture");
        var node = Path.Combine(release, "node");
        var python = Path.Combine(release, "python");
        Directory.CreateDirectory(Path.Combine(node, "node_modules", "npm", "bin"));
        Directory.CreateDirectory(python);
        foreach (var file in new[] { "node.exe", "node_modules/npm/bin/npm-cli.js", "node_modules/npm/bin/npx-cli.js" })
            File.WriteAllText(Path.Combine(node, file), "fixture");
        File.WriteAllText(Path.Combine(python, "python.exe"), "fixture");
        Environment = new(new("24.22.1", node, Path.Combine(node, "node.exe")), new("3.14.9", python, Path.Combine(python, "python.exe")));
    }
    public void Dispose()
    {
        ManagedAiPaths.RejectReparsePoints(Paths.Root);
        if (Directory.Exists(Paths.Root)) Directory.Delete(Paths.Root, recursive: true);
    }
}
