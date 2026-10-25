using System.Text.Json;
using TableCloth.ManagedAi.OpenAi;
using TableCloth.ManagedAi.Windows;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class ProviderTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ProviderValidatesBeforeLoggingAndAlwaysCleansRunDirectory(bool malformed)
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var runner = new ResponseRunner(malformed ? "{}" : ValidationTests.Response(), fixture.Paths);
        var profile = new WindowsManagedAiProfile(fixture.Paths);
        var auth = new OpenAiCodexAuthManager(fixture.Paths, fixture.Installer, profile, runner);
        var skills = new OpenAiCodexSkillManager(fixture.Paths, fixture.Installer, profile, runner);
        var provider = new OpenAiCodexProvider(fixture.Paths, fixture.Installer, auth, profile, runner, new(), new(fixture.Paths), skills);
        if (malformed)
            await Assert.ThrowsExactlyAsync<ManagedAiException>(() => provider.SearchAsync(new("public fixture query"), null, CancellationToken.None));
        else
            Assert.HasCount(1, (await provider.SearchAsync(new("public fixture query"), null, CancellationToken.None)).Results);
        using var log = JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(fixture.Paths.Logs).Single()));
        Assert.AreEqual(malformed ? "InvalidStructuredOutput" : "Completed", log.RootElement.GetProperty("Outcome").GetString());
        Assert.HasCount(0, Directory.GetDirectories(fixture.Paths.Under("runs")));
        Assert.DoesNotContain("public fixture query", log.RootElement.GetRawText());
    }

    [TestMethod]
    public async Task ChatUsesPlainTextContractAndReportsObservedWebSearches()
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var runner = new ResponseRunner("[공식 홈페이지](https://example.com/)", fixture.Paths);
        var profile = new WindowsManagedAiProfile(fixture.Paths);
        var auth = new OpenAiCodexAuthManager(fixture.Paths, fixture.Installer, profile, runner);
        var skills = new OpenAiCodexSkillManager(fixture.Paths, fixture.Installer, profile, runner);
        var provider = new OpenAiCodexProvider(fixture.Paths, fixture.Installer, auth, profile, runner, new(), new(fixture.Paths), skills);
        var result = await provider.ChatAsync(new("공식 홈페이지", [], "fixture-model"), null, CancellationToken.None);
        Assert.AreEqual("fixture-model", result.Model);
        Assert.AreEqual("--model", runner.Invocation!.StartInfo.ArgumentList[0]);
        Assert.AreEqual("fixture-model", runner.Invocation.StartInfo.ArgumentList[1]);
        Assert.AreEqual("--profile", runner.Invocation.StartInfo.ArgumentList[2]);
        Assert.AreEqual(ManagedAiPaths.SkillProfileName, runner.Invocation.StartInfo.ArgumentList[3]);
        Assert.AreEqual(1, result.SearchCalls);
        Assert.AreEqual("https://example.com/", ChatMessageLinks.Parse(result.Text).Single().Link!.AbsoluteUri);
        Assert.DoesNotContain("--output-schema", runner.Invocation!.StartInfo.ArgumentList);
        Assert.Contains(runner.ExternalManifest.Replace("\\", "\\\\", StringComparison.Ordinal),
            File.ReadAllText(fixture.Paths.SkillConfiguration));
        Assert.HasCount(0, Directory.GetDirectories(fixture.Paths.Under("runs")));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ProviderConnectsInstalledToolsWithoutInstallingDuringARequest(bool structured)
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        using var tools = new ToolEnvironmentFixture();
        // Place the fixtures in the same managed root that the provider owns.
        var release = fixture.Paths.Under("runtimes", "skill-tools", "releases", "fixture");
        Directory.CreateDirectory(release);
        var node = Path.Combine(release, "node");
        var python = Path.Combine(release, "python");
        Directory.Move(tools.Environment.NodeJs.RootDirectory, node);
        Directory.Move(tools.Environment.Python.RootDirectory, python);
        var manager = new InstalledTools(new(new("24.22.1", node, Path.Combine(node, "node.exe")),
            new("3.14.9", python, Path.Combine(python, "python.exe"))));
        var runner = new ResponseRunner(structured ? ValidationTests.Response() : "Fixture answer", fixture.Paths);
        var profile = new WindowsManagedAiProfile(fixture.Paths);
        var auth = new OpenAiCodexAuthManager(fixture.Paths, fixture.Installer, profile, runner);
        var skills = new OpenAiCodexSkillManager(fixture.Paths, fixture.Installer, profile, runner);
        var provider = new OpenAiCodexProvider(fixture.Paths, fixture.Installer, auth, profile, runner, new(), new(fixture.Paths), skills, manager);
        if (structured) await provider.SearchAsync(new("fixture"), null, CancellationToken.None);
        else await provider.ChatAsync(new("fixture", []), null, CancellationToken.None);
        Assert.AreEqual(1, manager.Reads);
        Assert.AreEqual(0, manager.Installs);
        Assert.Contains(node, runner.Invocation!.StartInfo.Environment["PATH"]!);
        Assert.AreEqual(Path.Combine(python, "python.exe"), runner.Invocation.StartInfo.Environment["KSKILL_PYTHON"]);
        Assert.AreEqual(fixture.Paths.Profile, runner.Invocation.StartInfo.Environment["CODEX_HOME"]);
    }

    private sealed class InstalledTools(SkillRuntimeEnvironment environment) : IManagedAiSkillRuntimeManager
    {
        public int Reads;
        public int Installs;
        public Task<SkillRuntimeEnvironment?> GetActiveAsync(CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult<SkillRuntimeEnvironment?>(environment);
        }
        public Task<SkillRuntimeEnvironment> InstallAsync(IProgress<AiProgress>? progress, CancellationToken cancellationToken)
        {
            Installs++;
            throw new InvalidOperationException("A request cannot install tools.");
        }
    }

    private sealed class ResponseRunner(string message, ManagedAiPaths paths) : IJsonlProcessRunner
    {
        public ProcessRunSpec? Invocation;
        public string ExternalManifest => Path.Combine(paths.Root, "external", "SKILL.md");
        public Task<ProcessOutcome> RunAsync(ProcessRunSpec spec, Action<string> stdout, Action<string>? stderr, CancellationToken cancellationToken)
        {
            if (spec.StartInfo.ArgumentList.TakeLast(2).SequenceEqual(new[] { "login", "status" }))
                stderr?.Invoke("Logged in using ChatGPT");
            else if (spec.StartInfo.ArgumentList.Contains("app-server"))
            {
                Assert.Contains("skills/list", spec.Respond!("{\"id\":1,\"result\":{}}")!.Text!);
                var response = JsonSerializer.Serialize(new
                {
                    id = 2,
                    result = new
                    {
                        data = new[] { new { cwd = spec.StartInfo.WorkingDirectory,
                            skills = new[] { new { name = "external", description = "External skill", path = ExternalManifest,
                                scope = "user", enabled = true } }, errors = Array.Empty<object>() } }
                    }
                });
                Assert.IsTrue(spec.Respond(response)!.Close);
            }
            else
            {
                Invocation = spec;
                stdout("{\"type\":\"item.completed\",\"item\":{\"type\":\"web_search\"}}");
                stdout(JsonSerializer.Serialize(new { type = "item.completed", item = new { type = "agent_message", text = message } }));
                stdout("{\"type\":\"turn.completed\"}");
            }
            return Task.FromResult(new ProcessOutcome(0, 512, 32));
        }
    }
}
