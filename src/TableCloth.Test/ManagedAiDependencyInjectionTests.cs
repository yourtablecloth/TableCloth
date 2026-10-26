using Microsoft.Extensions.DependencyInjection;
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
        Assert.AreSame(concrete, contract);
        Assert.AreSame(concrete, services.GetRequiredService<WindowsManagedAiScriptRuntimeManager>());
        Assert.AreSame(contract, services.GetRequiredService<IManagedAiScriptRuntimeManager>());
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
        using var services = Compose(fixture.Paths, locations);
        var execution = services.GetRequiredService<IManagedAiSkillExecution>();

        var error = await Assert.ThrowsExactlyAsync<ManagedAiException>(() =>
            execution.PrepareAsync([fixture.Calculator], fixture.RunDirectory, CancellationToken.None));

        Assert.AreEqual(AiFailureCode.SkillExecutionHostUnavailable, error.Code);
        Assert.AreEqual(1, locations.ExecutableReads);
        Assert.IsFalse(Directory.Exists(fixture.Paths.Under("skill-execution")));
    }

    [TestMethod]
    public async Task SingletonExecutionCapturesInjectedHostOnceAndReachesScriptValidationWithoutRuntimeDownload()
    {
        using var fixture = new Fixture();
        var host = fixture.Paths.Under("injected-host", "TableCloth.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(host)!);
        File.WriteAllText(host, "Fixture apphost, never executed.");
        var script = Path.Combine(fixture.Calculator.Directory, "scripts", "calculate.py");
        Directory.CreateDirectory(Path.GetDirectoryName(script)!);
        File.WriteAllText(script, "# Deliberately unreviewed fixture script.");
        var locations = new Locations(host);
        using var services = Compose(fixture.Paths, locations);
        var execution = services.GetRequiredService<IManagedAiSkillExecution>();

        var first = await Assert.ThrowsExactlyAsync<ManagedAiException>(() =>
            execution.PrepareAsync([fixture.Calculator], fixture.RunDirectory, CancellationToken.None));
        Assert.AreEqual(AiFailureCode.BlockedByPolicy, first.Code);

        locations.Host = "relative/TableCloth.exe";
        var again = services.GetRequiredService<IManagedAiSkillExecution>();
        var second = await Assert.ThrowsExactlyAsync<ManagedAiException>(() =>
            again.PrepareAsync([fixture.Calculator], fixture.RunDirectory, CancellationToken.None));

        Assert.AreSame(execution, again);
        Assert.AreEqual(AiFailureCode.BlockedByPolicy, second.Code);
        Assert.AreEqual(1, locations.ExecutableReads);
        Assert.IsFalse(Directory.Exists(fixture.Paths.Under("skill-execution")));
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

    private static ServiceProvider Compose(ManagedAiPaths paths, ISharedLocations locations)
    {
        var services = new ServiceCollection();
        services.AddSingleton(paths);
        services.AddSingleton(locations);
        services.AddManagedAiWindows();
        return services.BuildServiceProvider();
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Paths = new(Path.Combine(Path.GetTempPath(), "tcloth-di-" + Guid.NewGuid().ToString("N")));
            RunDirectory = Paths.Under("runs", "fixture");
            Directory.CreateDirectory(RunDirectory);
            Calculator = new("tablecloth-calculators", "Calculator fixture", "Never executed",
                Paths.Under("profiles", "openai-codex", "codex-home", "skills", "tablecloth-calculators"), true);
        }

        public ManagedAiPaths Paths { get; }
        public string RunDirectory { get; }
        public AiSkill Calculator { get; }
        public void Dispose()
        {
            if (Directory.Exists(Paths.Root)) Directory.Delete(Paths.Root, recursive: true);
        }
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
