using System.Security.Cryptography;
using System.Text.Json;
using TableCloth.AppContainerPoc;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class RuntimeDependencyIntegrityTests
{
    [TestMethod]
    public async Task ChangedRuntimeDependencyIsRejectedBeforeInjectedExecutorRuns()
    {
        using var fixture = new InstallFixture();
        var (manifest, dependency) = CreateManifest(fixture.Paths);
        var calls = 0;
        var registry = await SkillExecutionRegistry.LoadAsync(manifest, (_, _) =>
        {
            calls++;
            return Task.FromResult(new SandboxRunResult(0, "{\"ok\":true}", "", false, false, true));
        }, CancellationToken.None);
        File.WriteAllText(dependency, "changed runtime dependency");
        using var input = JsonDocument.Parse("{\"operation\":\"discount.single\",\"price\":100,\"rate_percent\":20}");

        var result = await registry.ExecuteAsync("tablecloth-calculators", input.RootElement, CancellationToken.None);

        Assert.AreEqual(0, calls);
        Assert.AreEqual("blocked", result.Status);
    }

    [TestMethod]
    public async Task VerifiedDependencyRemainsLockedThroughoutInjectedExecutorThenIsReleased()
    {
        using var fixture = new InstallFixture();
        var (manifest, dependency) = CreateManifest(fixture.Paths);
        var registry = await SkillExecutionRegistry.LoadAsync(manifest, (_, _) =>
        {
            Assert.ThrowsExactly<IOException>(() => File.WriteAllText(dependency, "not allowed while running"));
            return Task.FromResult(new SandboxRunResult(0, "{\"ok\":true}", "", false, false, true));
        }, CancellationToken.None);
        using var input = JsonDocument.Parse("{\"operation\":\"discount.single\",\"price\":100,\"rate_percent\":20}");

        var result = await registry.ExecuteAsync("tablecloth-calculators", input.RootElement, CancellationToken.None);

        Assert.AreEqual("completed", result.Status);
        File.WriteAllText(dependency, "the completed execution released its handle");
    }

    private static (string Manifest, string Dependency) CreateManifest(ManagedAiPaths paths)
    {
        Directory.CreateDirectory(paths.Root);
        var executable = paths.Under("python.exe");
        var script = paths.Under("calculate.py");
        var dependency = paths.Under("python-runtime.dll");
        File.WriteAllText(executable, "injected-executor fixture, never launched");
        File.WriteAllText(script, "injected-executor fixture, never executed");
        File.WriteAllText(dependency, "expected dependency bytes");
        static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        var manifest = paths.Under("manifest.json");
        File.WriteAllText(manifest, JsonSerializer.Serialize(new
        {
            ownedRoot = paths.Root,
            pythonExecutable = executable,
            pythonSha256 = Hash(executable),
            skills = new[] { new { skillId = "tablecloth-calculators", kind = "python-json-stdin", scriptPath = script,
                sha256 = Hash(script), allowedOperations = new[] { "discount.single" }, requiresNetwork = false } },
            runtimeFiles = new[] { new { path = dependency, sha256 = Hash(dependency) } }
        }));
        return (manifest, dependency);
    }
}
