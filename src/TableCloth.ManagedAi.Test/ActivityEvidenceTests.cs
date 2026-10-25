using System.Text.Json;
using TableCloth.ManagedAi.OpenAi;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class ActivityEvidenceTests
{
    [TestMethod]
    public void McpLifecycleKeepsOneRecordAndOnlySafeIdentityMetadata()
    {
        var parser = new CodexJsonlParser();
        parser.Accept(Mcp("item.started", "item_1", "in_progress"));
        Assert.AreEqual(AiResponseActivityOutcome.Incomplete, parser.Activities.Single().Outcome);
        parser.Accept(Mcp("item.updated", "item_1", "in_progress"));
        parser.Accept(Mcp("item.completed", "item_1", "completed", new
        {
            content = new[] { new { type = "text", text = "private result C:\\private\\file.txt" } },
            structured_content = new { access_token = "private structured result" }
        }));
        parser.Accept(Mcp("item.started", "item_1", "in_progress"));
        parser.Accept(Mcp("item.completed", "item_1", "completed", new { content = Array.Empty<object>() }));

        var activity = parser.Activities.Single();
        Assert.AreEqual(new AiResponseActivity(AiResponseActivityKind.McpTool, "calculator-tool",
            AiResponseActivityOutcome.Completed, "fixture_server"), activity);
        Assert.IsFalse(parser.ActivityEvidenceIncomplete);
        var retained = JsonSerializer.Serialize(parser.Activities);
        Assert.DoesNotContain("private", retained);
        Assert.DoesNotContain("file.txt", retained);
        Assert.DoesNotContain("access_token", retained);
        Assert.DoesNotContain("arguments", retained);
    }

    [TestMethod]
    public void McpFailuresAreDistinctFromCompletedExchangesAndDoNotRegress()
    {
        var parser = new CodexJsonlParser();
        parser.Accept(Mcp("item.completed", "item_failed", "failed", new { content = Array.Empty<object>() }));
        parser.Accept(Mcp("item.completed", "item_error", "completed", new { content = Array.Empty<object>() },
            new { message = "private transport failure" }));
        parser.Accept(Mcp("item.completed", "item_isError", "completed", new { content = Array.Empty<object>(), isError = true }));
        parser.Accept(Mcp("item.completed", "item_is_error", "completed", new { content = Array.Empty<object>(), is_error = true }));
        parser.Accept(Mcp("item.started", "item_failed", "in_progress"));
        parser.Accept(Mcp("item.completed", "item_failed", "completed", new { content = Array.Empty<object>() }));

        Assert.HasCount(4, parser.Activities);
        Assert.IsTrue(parser.Activities.All(x => x.Outcome == AiResponseActivityOutcome.Failed));
        Assert.DoesNotContain("private", JsonSerializer.Serialize(parser.Activities));
    }

    [TestMethod]
    public void ContradictoryIdentityOrTerminalStatusMarksEvidenceIncomplete()
    {
        var parser = new CodexJsonlParser();
        parser.Accept(Mcp("item.completed", "item_1", "completed", new { content = Array.Empty<object>() }));
        parser.Accept(Mcp("item.completed", "item_1", "failed", tool: "different-tool"));
        Assert.AreEqual("calculator-tool", parser.Activities.Single().Name);
        Assert.AreEqual(AiResponseActivityOutcome.Completed, parser.Activities.Single().Outcome);
        Assert.IsTrue(parser.ActivityEvidenceIncomplete);

        parser.Accept(Mcp("item.completed", "item_1", "failed"));
        Assert.AreEqual(AiResponseActivityOutcome.Failed, parser.Activities.Single().Outcome);
    }

    [TestMethod]
    [DataRow("completed", "failed")]
    [DataRow("failed", "completed")]
    public void ConflictingTerminalOutcomesKeepFailureAndMarkEvidenceIncomplete(string first, string second)
    {
        var parser = new CodexJsonlParser();
        parser.Accept(Mcp("item.completed", "item_1", first, new { content = Array.Empty<object>() }));
        Assert.IsFalse(parser.ActivityEvidenceIncomplete);
        parser.Accept(Mcp("item.completed", "item_1", second, new { content = Array.Empty<object>() }));
        Assert.AreEqual(AiResponseActivityOutcome.Failed, parser.Activities.Single().Outcome);
        Assert.IsTrue(parser.ActivityEvidenceIncomplete);
    }

    [TestMethod]
    public void StartedOrUpdatedWithoutTerminalEventRemainsIncomplete()
    {
        var parser = new CodexJsonlParser();
        parser.Accept(Mcp("item.started", "item_1", "in_progress"));
        parser.Accept(Mcp("item.updated", "item_1", "in_progress"));
        Assert.AreEqual(AiResponseActivityOutcome.Incomplete, parser.Activities.Single().Outcome);
        Assert.IsFalse(parser.ActivityEvidenceIncomplete);
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"content\":null}")]
    [DataRow("{\"content\":{}}")]
    [DataRow("{\"content\":[],\"isError\":\"true\"}")]
    [DataRow("null")]
    public void MissingOrMalformedResultCannotProveCompletedExchange(string result)
    {
        var parser = new CodexJsonlParser();
        parser.Accept(Mcp("item.completed", "item_1", "completed", JsonSerializer.Deserialize<JsonElement>(result)));
        Assert.AreEqual(AiResponseActivityOutcome.Incomplete, parser.Activities.Single().Outcome);
        Assert.IsTrue(parser.ActivityEvidenceIncomplete);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("future_status")]
    [DataRow("in_progress")]
    public void MissingOrUnknownTerminalStatusCannotProveCompletedExchange(string? status)
    {
        var parser = new CodexJsonlParser();
        parser.Accept(Mcp("item.completed", "item_1", status, new { content = Array.Empty<object>() }));
        Assert.AreEqual(AiResponseActivityOutcome.Incomplete, parser.Activities.Single().Outcome);
        Assert.IsTrue(parser.ActivityEvidenceIncomplete);
    }

    [TestMethod]
    [DataRow("tool", "C:\\private\\skill.py")]
    [DataRow("server", "https://user:password@example.com")]
    [DataRow("tool", "tool\nforged line")]
    [DataRow("tool", "스킬")]
    [DataRow("id", "")]
    public void UnsafeOrMissingIdentitiesAreSkippedWithoutRejectingTheAnswer(string field, string value)
    {
        var item = new Dictionary<string, object?>
        {
            ["type"] = "mcp_tool_call", ["id"] = "item_1", ["server"] = "fixture_server", ["tool"] = "calculator-tool",
            ["status"] = "completed", ["result"] = new { content = Array.Empty<object>() }
        };
        item[field] = value;
        var parser = new CodexJsonlParser();
        parser.Accept(JsonSerializer.Serialize(new { type = "item.completed", item }));
        CompleteAnswer(parser);
        Assert.HasCount(0, parser.Activities);
        Assert.IsTrue(parser.ActivityEvidenceIncomplete);
        Assert.AreEqual("Fixture answer", parser.CompleteText(0));
    }

    [TestMethod]
    public void ActivityCountAndIdentityLengthsAreBoundedAndSnapshotsStayStable()
    {
        var parser = new CodexJsonlParser();
        parser.Accept(Mcp("item.completed", "long_tool", "completed", new { content = Array.Empty<object>() }, tool: new string('a', 129)));
        Assert.HasCount(0, parser.Activities);
        for (var i = 0; i < 129; i++)
            parser.Accept(Mcp("item.started", $"item_{i}", "in_progress"));
        var before = parser.Activities;
        Assert.HasCount(128, before);
        Assert.IsTrue(parser.ActivityEvidenceIncomplete);
        parser.Accept(Mcp("item.completed", "item_0", "completed", new { content = Array.Empty<object>() }));
        Assert.AreEqual(AiResponseActivityOutcome.Incomplete, before[0].Outcome);
        Assert.AreEqual(AiResponseActivityOutcome.Completed, parser.Activities[0].Outcome);
    }

    [TestMethod]
    public void ModelAssertionsInstalledSkillsAndUnrelatedToolsNeverBecomeSkillEvidence()
    {
        var parser = new CodexJsonlParser();
        parser.Accept("{\"type\":\"item.completed\",\"item\":{\"type\":\"command_execution\",\"command\":\"python skill.py\",\"exit_code\":0}}");
        parser.Accept("{\"type\":\"item.completed\",\"item\":{\"type\":\"web_search\"}}");
        parser.Accept("{\"type\":\"item.completed\",\"item\":{\"type\":\"agent_message\",\"text\":\"I used tablecloth-calculators. used_skills=[calculator]\"}}");
        parser.Accept("{\"type\":\"turn.completed\"}");
        Assert.Contains("used_skills", parser.CompleteText(0));
        Assert.HasCount(0, parser.Activities);
        Assert.IsFalse(parser.ActivityEvidenceIncomplete);
        Assert.HasCount(0, new CodexJsonlParser().Activities);
        Assert.HasCount(0, new AiChatResponse("answer", DateTimeOffset.UtcNow, 0).Activities);
    }

    private static string Mcp(string type, string id, string? status, object? result = null, object? error = null,
        string tool = "calculator-tool") => JsonSerializer.Serialize(new
        {
            type,
            item = new { type = "mcp_tool_call", id, server = "fixture_server", tool, status,
                arguments = new { access_token = "private argument", path = "C:\\private\\file.txt" }, result, error }
        });

    private static void CompleteAnswer(CodexJsonlParser parser)
    {
        parser.Accept("{\"type\":\"item.completed\",\"item\":{\"type\":\"agent_message\",\"text\":\"Fixture answer\"}}");
        parser.Accept("{\"type\":\"turn.completed\"}");
    }
}
