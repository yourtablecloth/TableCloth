using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TableCloth.App.DependencyInjection;
using TableCloth.Components;
using TableCloth.ManagedAi;
using TableCloth.ManagedAi.Windows;

namespace TableCloth.Test;

[TestClass]
public sealed class ManagedAiDependencyInjectionTests
{
    [TestMethod]
    public void WindowsCompositionSharesRuntimeManagerAcrossItsConcreteAndPublicContracts()
    {
        using var fixture = new Fixture();
        var locations = new Locations(fixture.Paths.Under("TableCloth.exe"));
        using var services = Compose(fixture.Paths, locations);

        var concrete = services.GetRequiredService<WindowsManagedAiScriptRuntimeManager>();
        var contract = services.GetRequiredService<IManagedAiScriptRuntimeManager>();
        var executionRuntime = services.GetRequiredService<IManagedAiScriptExecutionRuntime>();
        Assert.AreSame(concrete, contract);
        Assert.AreSame(concrete, executionRuntime);
        Assert.AreSame(concrete, services.GetRequiredService<WindowsManagedAiScriptRuntimeManager>());
        Assert.AreSame(contract, services.GetRequiredService<IManagedAiScriptRuntimeManager>());
        Assert.AreSame(executionRuntime, services.GetRequiredService<IManagedAiScriptExecutionRuntime>());
        Assert.IsNull(services.GetService<IManagedAiSkillMarketplace>(), "The optional marketplace is not needed to compose the Windows runtime services.");
        var execution = services.GetRequiredService<IManagedAiSkillExecution>();
        Assert.AreSame(execution, services.GetRequiredService<IManagedAiSkillExecution>());
        Assert.AreSame(services.GetRequiredService<IJsonlProcessRunner>(), services.GetRequiredService<IJsonlProcessRunner>());
        Assert.AreSame(services.GetRequiredService<IManagedAiProfile>(), services.GetRequiredService<IManagedAiProfile>());
        Assert.AreEqual(1, locations.ExecutableReads);
        Assert.IsFalse(Directory.Exists(fixture.Paths.Under("skill-execution")));
    }

    [TestMethod]
    [DataRow("missing")]
    [DataRow("relative")]
    [DataRow("other-name")]
    [DataRow("empty")]
    public async Task InjectedUnavailableHostFailsBeforeDownloadingRuntimesWithoutUsingApplicationBaseHost(string scenario)
    {
        using var fixture = new Fixture();
        // The application reference copies its real apphost into the test output. It must not override DI.
        Assert.IsTrue(File.Exists(Path.Combine(AppContext.BaseDirectory, "TableCloth.exe")),
            "The fallback candidate must exist for this composition test.");
        var host = scenario switch
        {
            "missing" => fixture.Paths.Under("missing", "TableCloth.exe"),
            "relative" => "TableCloth.exe",
            "other-name" => fixture.Paths.Under("unrelated.exe"),
            "empty" => "",
            _ => throw new AssertFailedException("Unknown fixture scenario")
        };
        if (scenario == "other-name") File.WriteAllText(host, "Fixture executable, never executed.");
        var locations = new Locations(host);
        var runtime = new FixtureScriptRuntime(fixture.Paths);
        using var services = Compose(fixture.Paths, locations, runtime);
        var execution = services.GetRequiredService<IManagedAiSkillExecution>();

        var error = await Assert.ThrowsExactlyAsync<ManagedAiException>(() =>
            execution.PrepareAsync([fixture.Calculator], fixture.RunDirectory, CancellationToken.None));

        Assert.AreEqual(AiFailureCode.SkillExecutionHostUnavailable, error.Code);
        Assert.AreEqual(0, runtime.EnsureCalls);
        Assert.AreEqual(0, runtime.VerifiedFilesCalls);
        Assert.AreEqual(1, locations.ExecutableReads);
        Assert.IsFalse(Directory.Exists(fixture.Paths.Under("skill-execution")));
    }

    [TestMethod]
    public async Task SingletonExecutionCapturesInjectedHostOnceAndRegistersUpdatedScriptsThroughTheInjectedRuntime()
    {
        using var fixture = new Fixture();
        var host = fixture.Paths.Under("injected-host", "TableCloth.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(host)!);
        File.WriteAllText(host, "Fixture apphost, never executed.");
        var script = Path.Combine(fixture.Calculator.Directory, "scripts", "calculate.py");
        Directory.CreateDirectory(Path.GetDirectoryName(script)!);
        File.WriteAllText(script, "# First installed fixture version; never executed.", new UTF8Encoding(false));
        var locations = new Locations(host);
        var runtime = new FixtureScriptRuntime(fixture.Paths);
        using var services = Compose(fixture.Paths, locations, runtime);
        var execution = services.GetRequiredService<IManagedAiSkillExecution>();

        using var first = await execution.PrepareAsync([fixture.Calculator], fixture.RunDirectory, CancellationToken.None);
        Assert.IsNotNull(first);
        var firstHash = Hash(script);
        var firstManifest = JsonDocument.Parse(File.ReadAllText(first.Arguments[1]));
        using (firstManifest)
        {
            var registration = firstManifest.RootElement.GetProperty("skills")[0];
            Assert.AreEqual("python-script", registration.GetProperty("kind").GetString());
            Assert.AreEqual(firstHash, registration.GetProperty("sha256").GetString());
        }

        locations.Host = "relative/TableCloth.exe";
        File.WriteAllText(script, "# Updated installed fixture version; never executed.", new UTF8Encoding(false));
        var again = services.GetRequiredService<IManagedAiSkillExecution>();
        using var second = await again.PrepareAsync([fixture.Calculator], fixture.RunDirectory, CancellationToken.None);

        Assert.IsNotNull(second);
        Assert.AreSame(execution, again);
        Assert.AreSame(runtime, services.GetRequiredService<IManagedAiScriptExecutionRuntime>());
        Assert.AreEqual(host, first.Command);
        Assert.AreEqual(host, second.Command);
        Assert.AreNotEqual(first.SnapshotDirectory, second.SnapshotDirectory);
        using var secondManifest = JsonDocument.Parse(File.ReadAllText(second.Arguments[1]));
        var secondRegistration = secondManifest.RootElement.GetProperty("skills")[0];
        Assert.AreEqual(Hash(script), secondRegistration.GetProperty("sha256").GetString());
        Assert.AreNotEqual(firstHash, secondRegistration.GetProperty("sha256").GetString());
        Assert.AreNotEqual(WindowsManagedAiSkillExecution.CalculatorSha256, secondRegistration.GetProperty("sha256").GetString());
        Assert.AreEqual(1, locations.ExecutableReads);
        Assert.AreEqual(2, runtime.EnsureCalls);
        Assert.AreEqual(2, runtime.VerifiedFilesCalls);
        first.Dispose();
        Assert.IsFalse(File.Exists(first.Arguments[1]));
        Assert.IsFalse(Directory.Exists(first.SnapshotDirectory));
        Assert.IsTrue(File.Exists(second.Arguments[1]));
        Assert.IsTrue(File.Exists(script));
    }

    [TestMethod]
    public void WindowsCompositionRequiresSharedLocationsInsteadOfInferringTheCurrentProcessHost()
    {
        using var fixture = new Fixture();
        var collection = new ServiceCollection();
        collection.AddSingleton(fixture.Paths);
        collection.AddManagedAiWindows();
        using var services = collection.BuildServiceProvider();

        Assert.ThrowsExactly<InvalidOperationException>(() => services.GetRequiredService<IManagedAiSkillExecution>());
        Assert.IsFalse(Directory.Exists(fixture.Paths.Under("skill-execution")));
    }

    private static ServiceProvider Compose(ManagedAiPaths paths, ISharedLocations locations,
        IManagedAiScriptExecutionRuntime? runtime = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(paths);
        services.AddSingleton(locations);
        services.AddManagedAiWindows();
        if (runtime is not null) services.AddSingleton<IManagedAiScriptExecutionRuntime>(runtime);
        return services.BuildServiceProvider();
    }

    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Paths = new(Path.Combine(Path.GetTempPath(), "tcloth-di-" + Guid.NewGuid().ToString("N")));
            RunDirectory = Paths.Under("runs", "fixture");
            Directory.CreateDirectory(RunDirectory);
            Calculator = new("tablecloth-calculators", "Calculator fixture", "Never executed",
                Paths.Under("profiles", "openai-codex", "codex-home", "skills", "tablecloth-calculators"), true);
            var script = Path.Combine(Calculator.Directory, "scripts", "calculate.py");
            Directory.CreateDirectory(Path.GetDirectoryName(script)!);
            File.WriteAllText(script, "# Synthetic installed entry point, never executed.", new UTF8Encoding(false));
        }

        public ManagedAiPaths Paths { get; }
        public string RunDirectory { get; }
        public AiSkill Calculator { get; }
        public void Dispose()
        {
            if (Directory.Exists(Paths.Root)) Directory.Delete(Paths.Root, recursive: true);
        }
    }

    // Four-byte fixture executables provide verifiable files only. No test launches them
    // or resolves the real dedicated runtime manager's download/probe methods.
    private sealed class FixtureScriptRuntime(ManagedAiPaths paths) : IManagedAiScriptExecutionRuntime
    {
        public int EnsureCalls;
        public int VerifiedFilesCalls;
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
                File.WriteAllBytes(file, [0x54, 0x43, 0x44, 0x00]);
            }
            return Task.FromResult(Status());
        }
        public Task<IReadOnlyList<ScriptRuntimeFile>> GetVerifiedFilesAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            VerifiedFilesCalls++;
            return Task.FromResult<IReadOnlyList<ScriptRuntimeFile>>([new(PythonExecutable, Hash(PythonExecutable)), new(NodeExecutable, Hash(NodeExecutable))]);
        }
        private static AiScriptRuntimeStatus Status() => new(new("Python", "fixture", "fixture", true, null),
            new("Node.js", "fixture", "fixture", true, null), new(null, null, null));
    }

    private sealed class Locations(string host) : ISharedLocations
    {
        public string Host = host;
        public int ExecutableReads;
        public string ExecutableFilePath { get { ExecutableReads++; return Host; } }
        public string AppDataDirectoryPath => throw new NotSupportedException();
        public string ApplicationLogPath => throw new NotSupportedException();
        public string ExecutableDirectoryPath => throw new NotSupportedException();
        public string ImagesZipFilePath => throw new NotSupportedException();
        public string PreferencesFilePath => throw new NotSupportedException();
        public string CatalogCacheFilePath => throw new NotSupportedException();
        public string DefaultDataDirectoryPath => throw new NotSupportedException();
        public string GetImageDirectoryPath() => throw new NotSupportedException();
        public string GetImageFilePath(string serviceId) => throw new NotSupportedException();
        public string GetIconFilePath(string serviceId) => throw new NotSupportedException();
        public string GetTempPath() => throw new NotSupportedException();
        public string GetHelpFilePath() => throw new NotSupportedException();
        public string GetSandboxAppStagingPath(string sandboxStagingDirectory) => throw new NotSupportedException();
        public string GetEffectiveDataDirectoryPath(string? configuredPath) => throw new NotSupportedException();
        public bool IsMountableDataDirectory(string? path) => throw new NotSupportedException();
    }
}
