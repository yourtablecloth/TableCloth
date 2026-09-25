using System.Text.Json;
using TableCloth.ManagedAi.OpenAi;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class ChatTests
{
    [TestMethod]
    [DataRow("공식 [식탁보](https://yourtablecloth.app/) 링크", "식탁보", "https://yourtablecloth.app/")]
    [DataRow("참고: https://example.com/path?q=1&x=2.", "https://example.com/path?q=1&x=2", "https://example.com/path?q=1&x=2")]
    [DataRow("<http://example.com/>", "http://example.com/", "http://example.com/")]
    [DataRow("[항목](https://example.com/wiki/Item_(name))", "항목", "https://example.com/wiki/Item_(name)")]
    [DataRow("(https://example.com/path).", "https://example.com/path", "https://example.com/path")]
    public void RendersMarkdownAutolinksAndBareWebUrls(string text, string label, string destination)
    {
        var link = ChatMessageLinks.Parse(text).Single(x => x.Link is not null);
        Assert.AreEqual(label, link.Text);
        Assert.AreEqual(destination, link.Link!.AbsoluteUri);
    }

    [TestMethod]
    [DataRow("[로컬](http://127.0.0.1/)")]
    [DataRow("https://user:password@example.com/")]
    [DataRow("<https://[::1]/>")]
    [DataRow("file:///C:/private")]
    [DataRow("[실행](javascript:alert(1))")]
    [DataRow("http://intranet.internal/")]
    public void UnsafeLinksRemainUnmodifiedText(string text)
    {
        var parts = ChatMessageLinks.Parse(text);
        Assert.IsTrue(parts.All(x => x.Link is null));
        Assert.AreEqual(text, string.Concat(parts.Select(x => x.Text)));
    }

    [TestMethod]
    public void KeepsMultipleLinksAndRejectsOversizedMessages()
    {
        var parts = ChatMessageLinks.Parse("[하나](https://a.example/) 및 [둘](http://b.example/)");
        Assert.AreEqual(2, parts.Count(x => x.Link is not null));
        Assert.AreEqual("하나 및 둘", string.Concat(parts.Select(x => x.Text)));
        Assert.ThrowsExactly<ManagedAiException>(() => ChatMessageLinks.Parse(new string('x', 65537)));
    }

    [TestMethod]
    public async Task ConversationKeepsBoundedContextAndClickOpensAnyPublicLink()
    {
        var provider = new ChatProvider();
        var browser = new Browser();
        var session = new ManagedAiChatSession(provider, browser);
        await session.SendAsync("첫 질문", null, CancellationToken.None);
        await session.SendAsync("후속 질문", null, CancellationToken.None);
        Assert.HasCount(2, provider.Requests[1].History);
        Assert.AreEqual("첫 질문", provider.Requests[1].History[0].Text);
        Assert.AreEqual(AiChatRole.Assistant, provider.Requests[1].History[1].Role);
        Assert.IsNull(browser.Opened);
        // No latest-result membership gate: old assistant links and user-supplied links work alike.
        await session.OpenLinkAsync(new("http://different.example/service"), CancellationToken.None);
        Assert.AreEqual("http://different.example/service", browser.Opened!.AbsoluteUri);
        await Assert.ThrowsExactlyAsync<ManagedAiException>(() => session.OpenLinkAsync(new("https://localhost/"), CancellationToken.None));
        Assert.AreEqual("http://different.example/service", browser.Opened.AbsoluteUri);
        for (int i = 0; i < 12; i++) await session.SendAsync(new string('가', 3900), null, CancellationToken.None);
        Assert.IsTrue(session.History.Count <= 12 && session.History.Sum(x => x.Text.Length) <= 20000);
        Assert.AreEqual(AiChatRole.User, session.History[0].Role);
        session.Clear();
        Assert.HasCount(0, session.History);
    }

    [TestMethod]
    public async Task FailedTurnDoesNotPolluteFollowupContext()
    {
        var provider = new ChatProvider();
        var session = new ManagedAiChatSession(provider, new Browser());
        await session.SendAsync("첫 질문", null, CancellationToken.None);
        provider.Fail = true;
        await Assert.ThrowsExactlyAsync<ManagedAiException>(() => session.SendAsync("실패할 질문", null, CancellationToken.None));
        Assert.HasCount(2, session.History);
        provider.Fail = false;
        await session.SendAsync("재시도", null, CancellationToken.None);
        Assert.IsFalse(provider.Requests[^1].History.Any(x => x.Text == "실패할 질문"));
    }

    [TestMethod]
    public void ProgressTracksSearchAndWritingWithoutExposingReasoningContent()
    {
        var parser = new CodexJsonlParser();
        Assert.AreEqual("Thinking", parser.Stage);
        parser.Accept("{\"type\":\"item.started\",\"item\":{\"type\":\"web_search\"}}");
        Assert.AreEqual("Searching", parser.Stage);
        parser.Accept("{\"type\":\"item.completed\",\"item\":{\"type\":\"web_search\"}}");
        Assert.AreEqual(1, parser.SearchCalls);
        Assert.AreEqual("Thinking", parser.Stage);
        parser.Accept("{\"type\":\"item.updated\",\"item\":{\"type\":\"agent_message\"}}");
        Assert.AreEqual("Writing", parser.Stage);
    }

    [TestMethod]
    public void ChatAllowsGreetingWithoutSearchAndTransientErrorRecovery()
    {
        var parser = new CodexJsonlParser();
        parser.Accept("{\"type\":\"error\",\"message\":\"Reconnecting after connection failure\"}");
        parser.Accept(JsonSerializer.Serialize(new { type = "item.completed", item = new { type = "agent_message", text = "안녕하세요." } }));
        parser.Accept("{\"type\":\"turn.completed\"}");
        Assert.AreEqual("안녕하세요.", parser.CompleteText(0));
        Assert.AreEqual(0, parser.SearchCalls);
        Assert.AreEqual(AiFailureCode.LiveSearchNotObserved, Assert.ThrowsExactly<ManagedAiException>(() => parser.Complete(0, 5)).Code);
    }

    [TestMethod]
    [DataRow("invalid_json_schema: 'uri' is not a valid format", AiFailureCode.ProviderRequestRejected)]
    [DataRow("401 Unauthorized", AiFailureCode.AuthenticationRequired)]
    [DataRow("usage_limit_reached", AiFailureCode.SubscriptionUnavailable)]
    [DataRow("error sending request: connection reset", AiFailureCode.ProviderNetworkUnavailable)]
    [DataRow("unexpected argument --strict-config", AiFailureCode.ProviderConfigurationInvalid)]
    public void ProviderFailuresUseFixedActionableCodes(string error, AiFailureCode expected)
    {
        var parser = new CodexJsonlParser();
        parser.Accept(JsonSerializer.Serialize(new { type = "turn.failed", error = new { message = error } }));
        var exception = Assert.ThrowsExactly<ManagedAiException>(() => parser.CompleteText(1));
        Assert.AreEqual(expected, exception.Code);
        Assert.AreEqual(expected.ToString(), exception.Message);
    }

    [TestMethod]
    public void StderrClassifiesFailureButDoesNotOverrideSuccessfulTurn()
    {
        var parser = new CodexJsonlParser();
        parser.AcceptDiagnostic("network connection temporarily unavailable");
        Assert.AreEqual(AiFailureCode.ProviderNetworkUnavailable, Assert.ThrowsExactly<ManagedAiException>(() => parser.CompleteText(1)).Code);
        parser.Accept("{\"type\":\"item.completed\",\"item\":{\"type\":\"agent_message\",\"text\":\"ok\"}}");
        parser.Accept("{\"type\":\"turn.completed\"}");
        Assert.AreEqual("ok", parser.CompleteText(0));
    }

    [TestMethod]
    public void ChatPromptCarriesContextThroughStdinWithoutOutputSchema()
    {
        var request = new AiChatRequest("이 링크도 알려주세요.", [new(AiChatRole.User, "이전 질문"), new(AiChatRole.Assistant, "이전 응답")]);
        var prompt = OpenAiChatPromptFactory.Create(request);
        Assert.Contains("이전 질문", prompt);
        Assert.Contains("이전 응답", prompt);
        var root = Path.GetFullPath(".");
        var runtime = new ManagedRuntime(new("1.2.3", "x86_64-pc-windows-msvc", new string('a', 64)), root,
            Path.Combine(root, "codex.exe"), Path.Combine(root, "profile"), DateTimeOffset.UtcNow);
        var spec = OpenAiCodexInvocationBuilder.Chat(runtime, root, prompt, new());
        Assert.AreEqual(prompt, spec.Input);
        Assert.DoesNotContain("--output-schema", spec.StartInfo.ArgumentList);
        Assert.DoesNotContain(request.Message, spec.StartInfo.ArgumentList);
        Assert.Contains("--ephemeral", spec.StartInfo.ArgumentList);
        Assert.ThrowsExactly<ManagedAiException>(() => OpenAiChatPromptFactory.Create(new("a\0b", [])));
        Assert.ThrowsExactly<ManagedAiException>(() => OpenAiChatPromptFactory.Create(new("ok", Enumerable.Repeat(new AiChatMessage(AiChatRole.User, "old"), 13).ToArray())));
    }

    [TestMethod]
    public void EmbeddedSearchSchemaOmitsUnsupportedUriFormat()
    {
        using var stream = typeof(OpenAiCodexProvider).Assembly.GetManifestResourceStream("TableCloth.ManagedAi.OpenAi.Schemas.search-result.schema.json")!;
        using var json = JsonDocument.Parse(stream);
        var properties = json.RootElement.GetProperty("properties").GetProperty("results").GetProperty("items").GetProperty("properties");
        foreach (var field in new[] { "target_url", "source_url" })
        {
            Assert.AreEqual("string", properties.GetProperty(field).GetProperty("type").GetString());
            Assert.IsFalse(properties.GetProperty(field).TryGetProperty("format", out _));
        }
    }

    private sealed class ChatProvider : IManagedAiChatProvider
    {
        public List<AiChatRequest> Requests = [];
        public bool Fail;
        public Task<AiChatResponse> ChatAsync(AiChatRequest request, IProgress<AiProgress>? progress, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (Fail) throw new ManagedAiException(AiFailureCode.ProviderNetworkUnavailable);
            return Task.FromResult(new AiChatResponse("[안내](https://example.com/)", DateTimeOffset.UtcNow, 1));
        }
    }
    private sealed class Browser : ITableClothBrowser
    {
        public Uri? Opened;
        public Task OpenAsync(Uri target, CancellationToken cancellationToken) { Opened = target; return Task.CompletedTask; }
    }
}
