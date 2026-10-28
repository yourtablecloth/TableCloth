using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using TableCloth.ManagedAi.OpenAi;
using TableCloth.ManagedAi.Windows;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class SkillExecutionProvenanceTests
{
    private static AiSkillToolRegistration Registration => new("custom-tool", "installed.id", "Readable skill", "scripts/run.py");

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CompletedBrokerResultUsesRegisteredIdentityAndAcceptsCliOmittedFalse(bool includeIsError)
    {
        var parser = new CodexJsonlParser([Registration]);
        CompleteCall(parser, "item_1", result: Result(isError: includeIsError ? false : null));

        var record = parser.SkillExecutions.Single();
        Assert.IsTrue(record.Succeeded);
        Assert.AreEqual("installed.id", record.SkillId);
        Assert.AreEqual("Readable skill", record.SkillName);
        Assert.AreEqual("scripts/run.py", record.EntryPoint);
        Assert.AreEqual("custom-tool", record.ToolName);
        Assert.AreSame(record, parser.LatestSkillExecution);
        var retained = JsonSerializer.Serialize(record);
        Assert.DoesNotContain("private fixture argument", retained);
        Assert.DoesNotContain("private worker output", retained);
        Assert.DoesNotContain("private worker error", retained);
        AiSkillExecutionHistory.Validate(parser.SkillExecutions);
    }

    [TestMethod]
    [DataRow("failed", 7, true, false, false, "worker-exit")]
    [DataRow("failed", 0, true, true, false, "timeout")]
    [DataRow("failed", 0, true, false, true, "output-limit")]
    [DataRow("failed", 0, true, false, false, "cleanup-failed")]
    [DataRow("blocked", 0, false, false, false, "appcontainer-required")]
    public void WorkerFailureCannotBeOverriddenByOuterRpcCompletionOrValidLookingStdout(string status, int exit,
        bool contained, bool timedOut, bool limited, string failure)
    {
        var parser = new CodexJsonlParser([Registration]);
        var execution = Execution();
        execution["status"] = status; execution["exitCode"] = exit; execution["wasAppContainer"] = contained;
        execution["timedOut"] = timedOut; execution["outputLimited"] = limited; execution["failure"] = failure;
        CompleteCall(parser, "call", result: Result(execution, true));

        var record = parser.SkillExecutions.Single();
        Assert.IsFalse(record.Succeeded);
        Assert.AreEqual(status == "failed" ? AiSkillExecutionStatus.Failed : AiSkillExecutionStatus.Blocked, record.Status);
        Assert.AreEqual(failure, record.Failure);
        Assert.AreEqual(exit, record.ExitCode);
    }

    [TestMethod]
    [DataRow("isError")]
    [DataRow("exitCode")]
    [DataRow("wasAppContainer")]
    [DataRow("timedOut")]
    [DataRow("outputLimited")]
    [DataRow("failure")]
    [DataRow("missing-field")]
    [DataRow("wrong-type")]
    [DataRow("multiple-blocks")]
    [DataRow("invalid-json")]
    public void ContradictoryOrMalformedExecutionNeverBecomesSuccess(string change)
    {
        var parser = new CodexJsonlParser([Registration]);
        var execution = Execution();
        if (change == "exitCode") execution[change] = 5;
        if (change == "wasAppContainer") execution[change] = false;
        if (change is "timedOut" or "outputLimited") execution[change] = true;
        if (change == "failure") execution[change] = "cleanup-failed";
        if (change == "missing-field") execution.Remove("stderr");
        if (change == "wrong-type") execution["wasAppContainer"] = "true";
        var result = Result(execution, change == "isError" ? true : null);
        if (change == "multiple-blocks") result["content"]!.AsArray().Add(new JsonObject { ["type"] = "text", ["text"] = "extra" });
        if (change == "invalid-json") result["content"]![0]!["text"] = "not JSON";
        CompleteCall(parser, "call", result: result);

        Assert.AreEqual(AiSkillExecutionStatus.Unverified, parser.SkillExecutions.Single().Status);
        Assert.IsFalse(parser.SkillExecutions.Single().Succeeded);
    }

    [TestMethod]
    [DataRow("missing-start")]
    [DataRow("arguments")]
    [DataRow("duplicate-start")]
    [DataRow("start-error")]
    [DataRow("start-result")]
    [DataRow("completion-error")]
    [DataRow("completion-status")]
    public void TrustedCompletionRequiresOneMatchingStartedCall(string change)
    {
        var parser = new CodexJsonlParser([Registration]);
        var started = Item("call");
        if (change == "start-error") started["error"] = new JsonObject { ["message"] = "untrusted failure detail" };
        if (change == "start-result") started["result"] = Result();
        if (change != "missing-start") Accept(parser, "item.started", started);
        if (change == "duplicate-start") Accept(parser, "item.started", Item("call"));
        var completed = Item("call", result: Result());
        if (change == "arguments") completed["arguments"] = new JsonObject { ["stdin"] = "different argument" };
        if (change == "completion-error") completed["error"] = new JsonObject { ["message"] = "untrusted failure detail" };
        if (change == "completion-status") completed["status"] = "failed";
        Accept(parser, "item.completed", completed);

        Assert.AreEqual(AiSkillExecutionStatus.Unverified, parser.SkillExecutions.Single().Status);
        Assert.DoesNotContain("untrusted failure detail", JsonSerializer.Serialize(parser.SkillExecutions));
    }

    [TestMethod]
    public void UnregisteredToolOtherServerAndAssistantClaimsNeverSupplyExecutionEvidence()
    {
        var parser = new CodexJsonlParser([Registration]);
        CompleteCall(parser, "unknown", "unknown-tool");
        CompleteCall(parser, "other", server: "another_server");
        Accept(parser, "item.completed", new JsonObject { ["type"] = "agent_message", ["text"] = "I ran Readable skill in AppContainer successfully." });
        parser.Accept("{\"type\":\"turn.completed\"}");
        Assert.HasCount(0, parser.SkillExecutions);
        Assert.Contains("successfully", parser.CompleteText(0));
        var noRegistration = new CodexJsonlParser();
        CompleteCall(noRegistration, "call");
        Assert.HasCount(0, noRegistration.SkillExecutions);
    }

    [TestMethod]
    public void NormalizedToolAliasIsAcceptedOnlyWhenRegistrationIsUnambiguous()
    {
        var parser = new CodexJsonlParser([Registration]);
        CompleteCall(parser, "call", "custom_tool");
        Assert.IsTrue(parser.SkillExecutions.Single().Succeeded);
        Assert.AreEqual("custom-tool", parser.SkillExecutions.Single().ToolName);
        var ambiguous = new CodexJsonlParser([Registration, Registration with { ToolName = "custom_tool", SkillId = "other" }]);
        CompleteCall(ambiguous, "ambiguous", "custom_tool");
        Assert.HasCount(0, ambiguous.SkillExecutions);
        CompleteCall(ambiguous, "exact", "custom-tool");
        Assert.AreEqual("installed.id", ambiguous.SkillExecutions.Single().SkillId);
    }

    [TestMethod]
    public void DuplicateCompletionIsIdempotentAndUpdatesDoNotCommit()
    {
        var parser = new CodexJsonlParser([Registration]);
        Accept(parser, "item.started", Item("call"));
        Accept(parser, "item.updated", Item("call", result: Result()));
        Assert.HasCount(0, parser.SkillExecutions);
        Accept(parser, "item.completed", Item("call", result: Result()));
        Accept(parser, "item.completed", Item("call", result: Result()));
        Assert.HasCount(1, parser.SkillExecutions);
        Assert.IsNull(parser.LatestSkillExecution);
    }

    [TestMethod]
    public void ExecutionRecordsAndPendingArgumentsHaveExplicitBounds()
    {
        var parser = new CodexJsonlParser([Registration]);
        for (var index = 0; index < 20; index++) CompleteCall(parser, "item_" + index);
        Assert.HasCount(AiSkillExecutionHistory.MaximumRecords, parser.SkillExecutions);
        Assert.IsTrue(parser.SkillExecutionsTruncated);
        var pending = new CodexJsonlParser([Registration]);
        for (var index = 0; index < 200; index++) Accept(pending, "item.started", Item("pending_" + index));
        Assert.IsTrue(pending.SkillExecutionsTruncated);
        Accept(pending, "item.completed", Item("pending_0", result: Result()));
        Assert.IsTrue(pending.SkillExecutions.Single().Succeeded);
        var oversized = new CodexJsonlParser([Registration]);
        var start = Item("large");
        start["arguments"] = new JsonObject { ["stdin"] = new string('x', 131073) };
        Accept(oversized, "item.started", start);
        var completed = Item("large", result: Result());
        completed["arguments"] = start["arguments"]!.DeepClone();
        Accept(oversized, "item.completed", completed);
        Assert.AreEqual(AiSkillExecutionStatus.Unverified, oversized.SkillExecutions.Single().Status);
    }

    [TestMethod]
    public void InvalidRegistrationCannotIntroduceLabelsOrHostPathsIntoHistory()
    {
        foreach (var registration in new[] { Registration with { SkillName = "invalid\nlabel" },
            Registration with { EntryPoint = "C:/private/run.py" }, Registration with { EntryPoint = "../run.py" } })
        {
            var parser = new CodexJsonlParser([registration]);
            CompleteCall(parser, "call");
            Assert.HasCount(0, parser.SkillExecutions);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ProviderReturnsAndReportsObservedExecutionUsingPreparedBridge(bool legacyBridge)
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var runner = new ProvenanceRunner();
        var profile = new WindowsManagedAiProfile(fixture.Paths);
        var auth = new OpenAiCodexAuthManager(fixture.Paths, fixture.Installer, profile, runner);
        var skills = new OpenAiCodexSkillManager(fixture.Paths, fixture.Installer, profile, runner);
        var execution = new PreparedExecution(fixture.Paths, legacyBridge);
        var provider = new OpenAiCodexProvider(fixture.Paths, fixture.Installer, auth, profile, runner, new(), new(fixture.Paths), skills, execution);
        var updates = new List<AiProgress>();

        var result = await provider.ChatAsync(new("Run the installed skill.", []), new InlineProgress(updates.Add), CancellationToken.None);

        Assert.IsTrue(result.SkillExecutions.Single().Succeeded);
        Assert.AreEqual(legacyBridge ? "custom-tool" : "installed.id", result.SkillExecutions.Single().SkillId);
        Assert.AreEqual(1, updates.Count(update => update.SkillExecution?.Succeeded == true));
        Assert.HasCount(0, Directory.GetDirectories(fixture.Paths.Runs));
        Assert.IsFalse(result.SkillExecutionsTruncated);
    }

    [TestMethod]
    public async Task WindowsBridgeKeepsOriginalSkillNameIdAndEntryPointsForGeneratedToolNames()
    {
        using var fixture = new InstallFixture();
        var directory = fixture.Paths.Under("profiles", "openai-codex", "codex-home", "skills", "custom.id");
        Directory.CreateDirectory(Path.Combine(directory, "scripts"));
        File.WriteAllText(Path.Combine(directory, "SKILL.md"), "---\nname: readable-name\ndescription: Fixture.\n---\n");
        File.WriteAllText(Path.Combine(directory, "scripts", "first.py"), "print('fixture')");
        File.WriteAllText(Path.Combine(directory, "scripts", "second.js"), "console.log('fixture')");
        var skill = new AiSkill("custom.id", "Readable installed name", "Fixture.", directory, true);
        var host = fixture.Paths.Under("TableCloth.exe"); File.WriteAllBytes(host, [0x54, 0x43]);
        var run = fixture.Paths.Under("runs", "mapping"); Directory.CreateDirectory(run);
        using var bridge = await new WindowsManagedAiSkillExecution(fixture.Paths, new FixtureRuntime(fixture.Paths), host)
            .PrepareAsync([skill], run, CancellationToken.None);

        Assert.IsNotNull(bridge);
        Assert.HasCount(2, bridge.Tools);
        Assert.IsTrue(bridge.Tools.All(tool => tool.SkillId == "custom.id" && tool.SkillName == "Readable installed name"));
        CollectionAssert.AreEquivalent(new[] { "scripts/first.py", "scripts/second.js" }, bridge.Tools.Select(tool => tool.EntryPoint).ToArray());
        CollectionAssert.AreEqual(bridge.SkillIds.ToArray(), bridge.Tools.Select(tool => tool.ToolName).ToArray());
        Assert.IsTrue(bridge.Tools.All(tool => tool.ToolName != tool.SkillId));
    }

    private static void CompleteCall(CodexJsonlParser parser, string id, string tool = "custom-tool", JsonObject? result = null,
        string server = "tablecloth_skills")
    {
        Accept(parser, "item.started", Item(id, tool, server: server));
        Accept(parser, "item.completed", Item(id, tool, result ?? Result(), server));
    }
    private static void Accept(CodexJsonlParser parser, string type, JsonObject item)
        => parser.Accept(new JsonObject { ["type"] = type, ["item"] = item }.ToJsonString());
    private static JsonObject Item(string id, string tool = "custom-tool", JsonObject? result = null, string server = "tablecloth_skills")
        => new() { ["id"] = id, ["type"] = "mcp_tool_call", ["server"] = server, ["tool"] = tool,
            ["arguments"] = new JsonObject { ["stdin"] = "private fixture argument" }, ["result"] = result,
            ["error"] = null, ["status"] = result is null ? "in_progress" : "completed" };
    private static JsonObject Execution() => new() { ["status"] = "completed", ["exitCode"] = 0,
        ["stdout"] = "private worker output", ["stderr"] = "private worker error", ["wasAppContainer"] = true,
        ["timedOut"] = false, ["outputLimited"] = false, ["failure"] = null };
    private static JsonObject Result(JsonObject? execution = null, bool? isError = null)
    {
        var result = new JsonObject { ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = (execution ?? Execution()).ToJsonString() }),
            ["structured_content"] = null };
        if (isError is not null) result["isError"] = isError.Value;
        return result;
    }

    private sealed class InlineProgress(Action<AiProgress> report) : IProgress<AiProgress>
    { public void Report(AiProgress value) => report(value); }
    private sealed class PreparedExecution(ManagedAiPaths paths, bool legacy) : IManagedAiSkillExecution
    {
        public Task<AiSkillExecutionBridge?> PrepareAsync(IReadOnlyList<AiSkill> skills, string runDirectory, CancellationToken token)
            => Task.FromResult<AiSkillExecutionBridge?>(new(paths.Under("TableCloth.exe"), ["--skill-mcp", paths.Under("manifest.json")],
                paths.Root, ["custom-tool"]) { Tools = legacy ? [] : [Registration] });
    }
    private sealed class ProvenanceRunner : IJsonlProcessRunner
    {
        public Task<ProcessOutcome> RunAsync(ProcessRunSpec spec, Action<string> stdout, Action<string>? stderr, CancellationToken token)
        {
            if (spec.StartInfo.ArgumentList.TakeLast(2).SequenceEqual(new[] { "login", "status" })) stderr?.Invoke("Logged in using ChatGPT");
            else if (spec.StartInfo.ArgumentList.Contains("app-server"))
            {
                spec.Respond!("{\"id\":1,\"result\":{}}");
                spec.Respond(JsonSerializer.Serialize(new { id = 2, result = new { data = new[] {
                    new { cwd = spec.StartInfo.WorkingDirectory, skills = Array.Empty<object>(), errors = Array.Empty<object>() } } } }));
            }
            else
            {
                stdout(new JsonObject { ["type"] = "item.started", ["item"] = Item("call") }.ToJsonString());
                stdout(new JsonObject { ["type"] = "item.completed", ["item"] = Item("call", result: Result()) }.ToJsonString());
                stdout("{\"type\":\"item.completed\",\"item\":{\"type\":\"agent_message\",\"text\":\"Fixture answer without execution claims\"}}");
                stdout("{\"type\":\"turn.completed\"}");
            }
            return Task.FromResult(new ProcessOutcome(0, 512, 0));
        }
    }
    private sealed class FixtureRuntime(ManagedAiPaths paths) : IManagedAiScriptExecutionRuntime
    {
        public string PythonExecutable => paths.Under("skill-execution", "runtimes", "python", "python.exe");
        public string NodeExecutable => paths.Under("skill-execution", "runtimes", "node", "node.exe");
        public Task<AiScriptRuntimeStatus> GetStatusAsync(CancellationToken token) => Task.FromResult(Status());
        public Task<AiScriptRuntimeStatus> EnsureInstalledAsync(CancellationToken token)
        {
            foreach (var file in new[] { PythonExecutable, NodeExecutable })
            { Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllBytes(file, [0x54, 0x43]); }
            return Task.FromResult(Status());
        }
        public Task<IReadOnlyList<ScriptRuntimeFile>> GetVerifiedFilesAsync(CancellationToken token)
            => Task.FromResult<IReadOnlyList<ScriptRuntimeFile>>(new[] { PythonExecutable, NodeExecutable }.Select(file =>
                new ScriptRuntimeFile(file, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file))))).ToArray());
        private static AiScriptRuntimeStatus Status() => new(new("Python", "fixture", "fixture", true, null),
            new("Node.js", "fixture", "fixture", true, null), new(null, null, null));
    }
}
