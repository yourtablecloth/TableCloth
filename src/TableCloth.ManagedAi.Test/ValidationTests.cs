using System.Text.Json;
using TableCloth.ManagedAi.OpenAi;
using TableCloth.ManagedAi.Windows;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class ValidationTests
{
    internal static string Response(string target = "https://bank.example/service", string? source = null) => JsonSerializer.Serialize(new
    {
        answer = "공식 출처에서 확인했습니다.",
        results = new[] { new { title = "Example", target_url = target, description = "Description",
            source_name = "Official", source_url = source ?? "https://bank.example/", reason = "Reason" } },
        warnings = Array.Empty<string>()
    });

    [TestMethod]
    [DataRow("http://bank.example/")]
    [DataRow("javascript:alert(1)")]
    [DataRow("file:///C:/file")]
    [DataRow("https://localhost/")]
    [DataRow("https://foo.localhost/")]
    [DataRow("https://user:pass@bank.example/")]
    [DataRow("https://127.1/")]
    [DataRow("https://2130706433/")]
    [DataRow("https://10.0.0.1/")]
    [DataRow("https://172.31.255.255/")]
    [DataRow("https://192.168.0.1/")]
    [DataRow("https://169.254.169.254/")]
    [DataRow("https://[::1]/")]
    [DataRow("https://[::ffff:127.0.0.1]/")]
    [DataRow("https://[fc00::1]/")]
    [DataRow("https://[fe80::1]/")]
    [DataRow("https://bank.example\\@127.0.0.1/")]
    public void RejectsUnsafeUrl(string url) => Assert.IsFalse(PublicHttpsUrl.TryParse(url, out _));

    [TestMethod]
    public void ValidResultGetsHostTimestampAndUnsafeCandidateIsRemoved()
    {
        var before = DateTimeOffset.UtcNow;
        var result = SearchResultValidator.Validate(Response());
        Assert.HasCount(1, result.Results);
        Assert.IsTrue(result.RetrievedAtUtc >= before);
        var unsafeResult = SearchResultValidator.Validate(Response("https://127.0.0.1/"));
        Assert.HasCount(0, unsafeResult.Results);
        Assert.HasCount(1, unsafeResult.Warnings);
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"answer\":\"ok\",\"results\":[],\"warnings\":[],\"extra\":1}")]
    [DataRow("{\"answer\":\"ok\",\"answer\":\"x\",\"results\":[],\"warnings\":[]}")]
    [DataRow("{\"answer\":null,\"results\":[],\"warnings\":[]}")]
    [DataRow("{\"answer\":\"\",\"results\":[],\"warnings\":[]}")]
    public void RejectsMalformedSchema(string value) =>
        Assert.ThrowsExactly<ManagedAiException>(() => SearchResultValidator.Validate(value));

    [TestMethod]
    public void RejectsOversizedAndDuplicateCandidates()
    {
        var candidate = JsonDocument.Parse(Response()).RootElement.GetProperty("results")[0].GetRawText();
        var duplicates = "{\"answer\":\"ok\",\"results\":[" + candidate + "," + candidate + "],\"warnings\":[]}";
        Assert.HasCount(1, SearchResultValidator.Validate(duplicates).Results);
        var many = "{\"answer\":\"ok\",\"results\":[" + string.Join(',', Enumerable.Repeat(candidate, 6)) + "],\"warnings\":[]}";
        Assert.ThrowsExactly<ManagedAiException>(() => SearchResultValidator.Validate(many));
        Assert.ThrowsExactly<ManagedAiException>(() => SearchResultValidator.Validate(Response().Replace("Description", new string('x', 501))));
    }

    [TestMethod]
    public void ParserRequiresSearchCompletionAndValidFinalMessage()
    {
        var parser = new CodexJsonlParser();
        parser.Accept("{\"type\":\"future.event\",\"value\":\"ignored\"}");
        parser.Accept("42");
        parser.Accept("{\"type\":\"item.completed\",\"item\":{\"type\":\"web_search\"}}");
        parser.Accept(JsonSerializer.Serialize(new { type = "item.completed", item = new { type = "agent_message", text = Response() } }));
        Assert.ThrowsExactly<ManagedAiException>(() => parser.Complete(0, 5));
        parser.Accept("{\"type\":\"turn.completed\",\"usage\":{}}");
        Assert.HasCount(1, parser.Complete(0, 5).Results);
        Assert.AreEqual(2, parser.UnknownEvents);
        parser.Accept("{\"type\":\"turn.failed\",\"message\":\"private content\"}");
        Assert.AreEqual(AiFailureCode.ProviderFailed, Assert.ThrowsExactly<ManagedAiException>(() => parser.Complete(0, 5)).Code);
    }

    [TestMethod]
    public void ParserRejectsMalformedJsonAndMissingWebSearch()
    {
        var parser = new CodexJsonlParser();
        Assert.ThrowsExactly<ManagedAiException>(() => parser.Accept("not JSON"));
        parser.Accept(JsonSerializer.Serialize(new { type = "item.completed", item = new { type = "agent_message", text = Response() } }));
        parser.Accept("{\"type\":\"turn.completed\"}");
        Assert.AreEqual(AiFailureCode.LiveSearchNotObserved, Assert.ThrowsExactly<ManagedAiException>(() => parser.Complete(0, 5)).Code);
    }

    [TestMethod]
    public void QueryNeverBecomesAnArgumentAndCredentialsAreExcluded()
    {
        var root = Path.GetFullPath(".");
        var runtime = new ManagedRuntime(new("1.2.3", "x86_64-pc-windows-msvc", new string('a', 64)), root,
            Path.Combine(root, "codex.exe"), Path.Combine(root, "profile"), DateTimeOffset.UtcNow);
        var query = "'; Stop-Process -Name example; $(echo test)";
        var prompt = OpenAiSearchPromptFactory.Create(new(query));
        var spec = OpenAiCodexInvocationBuilder.Search(runtime, root, Path.Combine(root, "schema.json"), prompt, new());
        Assert.DoesNotContain(query, spec.StartInfo.ArgumentList);
        Assert.AreEqual(prompt, spec.Input);
        Assert.IsFalse(spec.StartInfo.Environment.ContainsKey("OPENAI_API_KEY"));
        Assert.IsFalse(spec.StartInfo.Environment.ContainsKey("CODEX_ACCESS_TOKEN"));
        Assert.AreEqual(runtime.ProfileDirectory, spec.StartInfo.Environment["CODEX_HOME"]);
        var arguments = spec.StartInfo.ArgumentList.ToArray();
        var profileIndex = Array.IndexOf(arguments, "--profile");
        var execIndex = Array.IndexOf(arguments, "exec");
        Assert.IsGreaterThanOrEqualTo(0, profileIndex);
        Assert.AreEqual(ManagedAiPaths.SkillProfileName, arguments[profileIndex + 1]);
        Assert.IsGreaterThan(profileIndex, execIndex);
        Assert.Contains("--ephemeral", spec.StartInfo.ArgumentList);
        Assert.ThrowsExactly<ManagedAiException>(() => OpenAiSearchPromptFactory.Create(new("a\0b")));
        Assert.IsLessThan(4000, OpenAiSearchPromptFactory.Create(new(new string('가', 2000))).Length);
    }

    [TestMethod]
    public async Task ResultsDoNotOpenBrowserUntilExplicitSelection()
    {
        var response = SearchResultValidator.Validate(Response());
        var browser = new FakeBrowser();
        var orchestrator = new ManagedAiOrchestrator(new FakeProvider(response), browser);
        var result = await orchestrator.SearchAsync(new("query"), null, CancellationToken.None);
        Assert.AreEqual(0, browser.Calls);
        await orchestrator.OpenSelectionAsync(result.Results[0], CancellationToken.None);
        Assert.AreEqual(1, browser.Calls);
        await Assert.ThrowsExactlyAsync<ManagedAiException>(() => orchestrator.OpenSelectionAsync(
            result.Results[0] with { TargetUrl = new Uri("https://other.example/") }, CancellationToken.None));
    }

    [TestMethod]
    public void BrowserSandboxUsesEncodedDataAndNoHostMappings()
    {
        var doc = System.Xml.Linq.XDocument.Parse(BrowserOnlySandboxSpec.Create(new("https://bank.example/?q=%27%3B%24%28x%29")));
        Assert.IsNull(doc.Root!.Element("MappedFolders"));
        Assert.AreEqual("Disable", doc.Root.Element("ClipboardRedirection")!.Value);
        var command = doc.Root.Element("LogonCommand")!.Element("Command")!.Value;
        var script = System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(command.Split(' ')[^1]));
        Assert.DoesNotContain("https://", script);
        Assert.Contains("FromBase64String", script);
    }

    private sealed class FakeBrowser : ITableClothBrowser
    {
        public int Calls;
        public Task OpenAsync(Uri target, CancellationToken cancellationToken) { Calls++; return Task.CompletedTask; }
    }
    private sealed class FakeProvider(AiSearchResponse response) : IManagedAiProvider
    {
        public Task<AiSearchResponse> SearchAsync(AiSearchRequest request, IProgress<AiProgress>? progress, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
