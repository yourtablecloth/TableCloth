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
        var runner = new ResponseRunner(malformed ? "{}" : ValidationTests.Response());
        var profile = new WindowsManagedAiProfile(fixture.Paths);
        var auth = new OpenAiCodexAuthManager(fixture.Paths, fixture.Installer, profile, runner);
        var provider = new OpenAiCodexProvider(fixture.Paths, fixture.Installer, auth, profile, runner, new(), new(fixture.Paths));
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
        var runner = new ResponseRunner("[공식 홈페이지](https://example.com/)");
        var profile = new WindowsManagedAiProfile(fixture.Paths);
        var auth = new OpenAiCodexAuthManager(fixture.Paths, fixture.Installer, profile, runner);
        var provider = new OpenAiCodexProvider(fixture.Paths, fixture.Installer, auth, profile, runner, new(), new(fixture.Paths));
        var result = await provider.ChatAsync(new("공식 홈페이지", [], "fixture-model"), null, CancellationToken.None);
        Assert.AreEqual("fixture-model", result.Model);
        Assert.AreEqual("--model", runner.Invocation!.StartInfo.ArgumentList[0]);
        Assert.AreEqual("fixture-model", runner.Invocation.StartInfo.ArgumentList[1]);
        Assert.AreEqual(1, result.SearchCalls);
        Assert.AreEqual("https://example.com/", ChatMessageLinks.Parse(result.Text).Single().Link!.AbsoluteUri);
        Assert.DoesNotContain("--output-schema", runner.Invocation!.StartInfo.ArgumentList);
        Assert.HasCount(0, Directory.GetDirectories(fixture.Paths.Under("runs")));
    }

    private sealed class ResponseRunner(string message) : IJsonlProcessRunner
    {
        public ProcessRunSpec? Invocation;
        public Task<ProcessOutcome> RunAsync(ProcessRunSpec spec, Action<string> stdout, Action<string>? stderr, CancellationToken cancellationToken)
        {
            if (spec.StartInfo.ArgumentList.SequenceEqual(new[] { "login", "status" }))
                stderr?.Invoke("Logged in using ChatGPT");
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
