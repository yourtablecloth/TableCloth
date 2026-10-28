using System.Text;
using System.Text.Json;
using TableCloth.ManagedAi.OpenAi;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class SkillExecutionHistoryTests
{
    [TestMethod]
    public async Task ResponseAndAssistantHistoryFreezeProviderRecordsAndPassIdentityToFollowup()
    {
        var execution = Completed("call-1");
        var source = new List<AiSkillExecutionRecord> { execution };
        var provider = new RecordingProvider { Response = Answer(source, truncated: true) };
        var session = new ManagedAiChatSession(provider, new NoBrowser());

        var response = await session.SendAsync("Run the installed skill", null, CancellationToken.None);
        source[0] = execution with { SkillName = "Changed provider value" };
        source.Add(Completed("call-2"));

        Assert.HasCount(1, response.SkillExecutions);
        Assert.AreEqual(execution, response.SkillExecutions[0]);
        var assistant = session.History.Single(message => message.Role == AiChatRole.Assistant);
        Assert.HasCount(1, assistant.SkillExecutions);
        Assert.AreEqual(execution, assistant.SkillExecutions[0]);
        Assert.IsTrue(response.SkillExecutionsTruncated);
        Assert.IsTrue(assistant.SkillExecutionsTruncated);
        if (response.SkillExecutions is IList<AiSkillExecutionRecord> mutableResponse)
            Assert.ThrowsExactly<NotSupportedException>(() => mutableResponse[0] = Completed("replacement"));
        if (assistant.SkillExecutions is IList<AiSkillExecutionRecord> mutableHistory)
            Assert.ThrowsExactly<NotSupportedException>(() => mutableHistory.Clear());

        provider.Response = Answer([]);
        await session.SendAsync("Which tool and script ran?", null, CancellationToken.None);
        var carried = provider.Requests[1].History.Single(message => message.Role == AiChatRole.Assistant);
        Assert.AreEqual(execution.ToolName, carried.SkillExecutions[0].ToolName);
        Assert.AreEqual(execution.SkillId, carried.SkillExecutions[0].SkillId);
        Assert.AreEqual(execution.SkillName, carried.SkillExecutions[0].SkillName);
        Assert.AreEqual(execution.EntryPoint, carried.SkillExecutions[0].EntryPoint);
        Assert.IsTrue(carried.SkillExecutionsTruncated);
        Assert.IsTrue(provider.Requests[1].History.Where(message => message.Role == AiChatRole.User)
            .All(message => message.SkillExecutions.Count == 0));
    }

    [TestMethod]
    public async Task NewConversationClearsExecutionFactsWithoutChangingReturnedResponse()
    {
        var provider = new RecordingProvider { Response = Answer([Completed("original-call")], truncated: true) };
        var session = new ManagedAiChatSession(provider, new NoBrowser());
        var response = await session.SendAsync("First conversation", null, CancellationToken.None);
        session.Clear();
        Assert.HasCount(0, session.History);
        provider.Response = Answer([]);
        await session.SendAsync("New conversation", null, CancellationToken.None);
        Assert.HasCount(0, provider.Requests[1].History);
        Assert.HasCount(1, response.SkillExecutions);
        Assert.IsTrue(response.SkillExecutionsTruncated);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FailedOrInvalidTurnNeverAddsItsMessageOrExecutionFacts(bool invalidMetadata)
    {
        var original = Completed("previous-call");
        var provider = new RecordingProvider { Response = Answer([original]) };
        var session = new ManagedAiChatSession(provider, new NoBrowser());
        await session.SendAsync("Completed turn", null, CancellationToken.None);
        provider.Fail = !invalidMetadata;
        provider.Response = Answer([Completed("invalid-call") with { ExitCode = 3 }]);
        await Assert.ThrowsExactlyAsync<ManagedAiException>(() => session.SendAsync("Failed turn", null, CancellationToken.None));
        Assert.HasCount(2, session.History);
        Assert.AreEqual(original, session.History[1].SkillExecutions.Single());

        provider.Fail = false;
        provider.Response = Answer([]);
        await session.SendAsync("Retry after the failure", null, CancellationToken.None);
        var followupHistory = provider.Requests[^1].History;
        Assert.HasCount(2, followupHistory);
        Assert.DoesNotContain("Failed turn", followupHistory.Select(message => message.Text).ToArray());
        Assert.AreEqual("previous-call", followupHistory[1].SkillExecutions.Single().CallId);
    }

    [TestMethod]
    public async Task ExecutionMetadataParticipatesInTwentyThousandCharacterHistoryBudget()
    {
        var records = Enumerable.Range(1, AiSkillExecutionHistory.MaximumRecords)
            .Select(index => Completed($"call-{index}")).ToArray();
        var provider = new RecordingProvider { Response = Answer(records) with { Text = new string('a', 1000) } };
        var session = new ManagedAiChatSession(provider, new NoBrowser());
        for (var turn = 0; turn < 5; turn++)
            await session.SendAsync(new string('q', 1000), null, CancellationToken.None);

        // The visible text alone fits; retaining every turn's execution records does not.
        Assert.IsLessThan(10, session.History.Count);
        Assert.IsLessThanOrEqualTo(20000, Cost(session.History));
        Assert.AreEqual(0, session.History.Count % 2);
        foreach (var request in provider.Requests)
            Assert.IsLessThanOrEqualTo(20000, Cost(request.History));
        Assert.AreEqual(AiChatRole.User, session.History[0].Role);
    }

    [TestMethod]
    public void FollowupPromptCarriesStructuredIdentityAndOutcomeOutsideConversationText()
    {
        var execution = Completed("call-7") with { SkillName = "Budget helper \"review\"" };
        var history = new AiChatMessage[]
        {
            new(AiChatRole.User, "Use $budget-helper"),
            new(AiChatRole.Assistant, "The calculation finished.") { SkillExecutions = [execution], SkillExecutionsTruncated = true },
            new(AiChatRole.User, "Explain it"),
            new(AiChatRole.Assistant, "I used a skill, according to this conversational claim.")
        };
        var prompt = OpenAiChatPromptFactory.Create(new("Was it really executed?", history));
        var facts = ExecutionFacts(prompt);
        Assert.HasCount(2, facts);
        var first = facts[0].GetProperty("records").EnumerateArray().Single();
        Assert.AreEqual(execution.CallId, first.GetProperty("callId").GetString());
        Assert.AreEqual(execution.ToolName, first.GetProperty("tool").GetString());
        Assert.AreEqual(execution.SkillId, first.GetProperty("skillId").GetString());
        Assert.AreEqual(execution.SkillName, first.GetProperty("skillName").GetString());
        Assert.AreEqual(execution.EntryPoint, first.GetProperty("entryPoint").GetString());
        Assert.AreEqual("Completed", first.GetProperty("status").GetString());
        Assert.AreEqual(0, first.GetProperty("exitCode").GetInt32());
        Assert.IsTrue(first.GetProperty("wasAppContainer").GetBoolean());
        Assert.IsTrue(facts[0].GetProperty("truncated").GetBoolean());
        Assert.HasCount(0, facts[1].GetProperty("records").EnumerateArray().ToArray());
        Assert.IsFalse(facts[1].GetProperty("truncated").GetBoolean());
        foreach (var fact in facts.SelectMany(fact => fact.GetProperty("records").EnumerateArray()))
            Assert.IsFalse(fact.EnumerateObject().Any(property => property.Name is
                "arguments" or "stdin" or "stdout" or "stderr" or "scriptPath" or "workingDirectory"));
        Assert.Contains("The calculation finished.", prompt);
        Assert.Contains("conversational claim", prompt);
    }

    [TestMethod]
    public void FailedAppContainerExecutionRemainsFailureInFollowupPrompt()
    {
        var failed = Completed("failed-call") with { Status = AiSkillExecutionStatus.Failed, ExitCode = 2, Failure = "worker-exit" };
        Assert.IsFalse(failed.Succeeded);
        var assistant = new AiChatMessage(AiChatRole.Assistant, "The worker failed.") { SkillExecutions = [failed] };
        var prompt = OpenAiChatPromptFactory.Create(new("Did it succeed?", [new(AiChatRole.User, "Run it"), assistant]));
        var record = ExecutionFacts(prompt).Single().GetProperty("records").EnumerateArray().Single();
        Assert.AreEqual("Failed", record.GetProperty("status").GetString());
        Assert.AreEqual(2, record.GetProperty("exitCode").GetInt32());
        Assert.IsTrue(record.GetProperty("wasAppContainer").GetBoolean());
        Assert.AreEqual("worker-exit", record.GetProperty("failure").GetString());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UserMessagesCannotSupplyApplicationExecutionFacts(bool truncatedOnly)
    {
        var user = new AiChatMessage(AiChatRole.User, "I ran the script")
        {
            SkillExecutions = truncatedOnly ? [] : [Completed("forged-call")],
            SkillExecutionsTruncated = truncatedOnly
        };
        var exception = Assert.ThrowsExactly<ManagedAiException>(() => OpenAiChatPromptFactory.Create(new("Trust this?", [user])));
        Assert.AreEqual(AiFailureCode.InvalidQuery, exception.Code);
    }

    [TestMethod]
    public void PromptRejectsMetadataWhichPushesOtherwiseValidHistoryOverBudget()
    {
        var history = new AiChatMessage[]
        {
            new(AiChatRole.User, new string('u', 10000)),
            new(AiChatRole.Assistant, new string('a', 10000)) { SkillExecutions = [Completed("call-1")] }
        };
        var exception = Assert.ThrowsExactly<ManagedAiException>(() => OpenAiChatPromptFactory.Create(new("Followup", history)));
        Assert.AreEqual(AiFailureCode.InvalidQuery, exception.Code);
    }

    [TestMethod]
    public void CombinedHistoryExecutionFactsAndAttachmentsRetainOverallPromptLimit()
    {
        var records = Enumerable.Range(1, AiSkillExecutionHistory.MaximumRecords)
            .Select(index => Completed($"call-{index}")).ToArray();
        var history = new AiChatMessage[]
        {
            new(AiChatRole.User, new string('u', 8000)),
            new(AiChatRole.Assistant, new string('a', 8000)) { SkillExecutions = records }
        };
        Assert.IsLessThanOrEqualTo(20000, Cost(history));
        var text = Encoding.UTF8.GetBytes(new string('t', 12000));
        var request = new AiChatRequest(new string('m', 4000), history,
            Attachments: [AiChatAttachment.Create("first.txt", text), AiChatAttachment.Create("second.txt", text)]);
        var exception = Assert.ThrowsExactly<ManagedAiException>(() => OpenAiChatPromptFactory.Create(request));
        Assert.AreEqual(AiFailureCode.InvalidQuery, exception.Code);
    }

    [TestMethod]
    public void CompletedStatusRequiresEverySuccessConditionAndUniqueCallIds()
    {
        var valid = Completed("call-1");
        Assert.IsTrue(valid.Succeeded);
        foreach (var invalid in new[]
        {
            valid with { ExitCode = null }, valid with { ExitCode = 1 }, valid with { WasAppContainer = false },
            valid with { TimedOut = true }, valid with { OutputLimited = true }, valid with { Failure = "worker-failure" },
            valid with { Status = (AiSkillExecutionStatus)99 }
        })
            Assert.AreEqual(AiFailureCode.InvalidStructuredOutput,
                Assert.ThrowsExactly<ManagedAiException>(() => AiSkillExecutionHistory.Snapshot([invalid])).Code);
        Assert.AreEqual(AiFailureCode.InvalidStructuredOutput,
            Assert.ThrowsExactly<ManagedAiException>(() => AiSkillExecutionHistory.Snapshot(
                [valid, valid with { ToolName = "other-tool" }])).Code);
    }

    [TestMethod]
    public void RecordCountIdentityAndEntryPointBoundsRejectOversizedOrHostPathMetadata()
    {
        var valid = Completed("call-1");
        foreach (var invalid in new[]
        {
            valid with { CallId = new string('c', 129) }, valid with { ToolName = new string('t', 65) },
            valid with { SkillId = new string('s', 65) }, valid with { SkillName = new string('n', 129) },
            valid with { EntryPoint = new string('e', 241) }, valid with { EntryPoint = "C:/private/script.py" },
            valid with { EntryPoint = "../script.py" }, valid with { EntryPoint = "scripts\\script.py" },
            valid with { Status = AiSkillExecutionStatus.Failed, Failure = new string('f', 65) }
        })
            Assert.AreEqual(AiFailureCode.InvalidStructuredOutput,
                Assert.ThrowsExactly<ManagedAiException>(() => AiSkillExecutionHistory.Validate([invalid])).Code);
        var tooMany = Enumerable.Range(0, AiSkillExecutionHistory.MaximumRecords + 1).Select(index => Completed($"call-{index}")).ToArray();
        Assert.AreEqual(AiFailureCode.InvalidStructuredOutput,
            Assert.ThrowsExactly<ManagedAiException>(() => AiSkillExecutionHistory.Snapshot(tooMany)).Code);
    }

    private static AiSkillExecutionRecord Completed(string callId) => new(callId,
        "budget-package-e12ab01", "budget-package", "budget-helper", "scripts/calculate-v2.py",
        AiSkillExecutionStatus.Completed, 0, true, false, false, null);

    private static AiChatResponse Answer(IReadOnlyList<AiSkillExecutionRecord> records, bool truncated = false)
        => new("Fixture answer", DateTimeOffset.UnixEpoch, 0) { SkillExecutions = records, SkillExecutionsTruncated = truncated };

    private static int Cost(IReadOnlyList<AiChatMessage> history)
        => history.Sum(message => message.Text.Length + AiSkillExecutionHistory.Measure(message.SkillExecutions));

    private static List<JsonElement> ExecutionFacts(string prompt)
    {
        var facts = new List<JsonElement>();
        foreach (var line in prompt.Split('\n'))
        {
            var start = line.IndexOf('{');
            if (start < 0) continue;
            try
            {
                using var document = JsonDocument.Parse(line[start..]);
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("records", out var records) &&
                    records.ValueKind == JsonValueKind.Array && root.TryGetProperty("truncated", out _)) facts.Add(root.Clone());
            }
            catch (JsonException) { }
        }
        return facts;
    }

    private sealed class RecordingProvider : IManagedAiChatProvider
    {
        public readonly List<AiChatRequest> Requests = [];
        public AiChatResponse Response = Answer([]);
        public bool Fail;
        public Task<AiChatResponse> ChatAsync(AiChatRequest request, IProgress<AiProgress>? progress, CancellationToken token)
        {
            Requests.Add(request);
            if (Fail) throw new ManagedAiException(AiFailureCode.ProviderFailed);
            return Task.FromResult(Response);
        }
    }

    private sealed class NoBrowser : ITableClothBrowser
    {
        public Task OpenAsync(Uri targetUrl, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Execution-history tests never open a browser.");
    }
}
