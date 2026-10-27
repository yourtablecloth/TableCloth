using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TableCloth.SkillSmokeDiagnostics;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class SkillEvidenceVerifierTests
{
    [TestMethod]
    public void ConsistentSyntheticOfflineBundlePassesWithoutTrustingSummaryFlags()
    {
        using var fixture = new EvidenceFixture();
        fixture.Write("summary.json", new { Passed = false, success = false });
        fixture.Write("bok-ecos-stats.chat.json", new { proof = new { Passed = false } });

        var report = EvidenceVerifier.Verify(fixture.Root, EvidenceFixture.ProductHash);

        Assert.IsTrue(report.Passed, string.Join(",", report.Failures));
        Assert.AreEqual("update-safe-data-root", report.RuntimeStorageLayout);
        Assert.IsFalse(report.HistoricalStorageLayout);
        Assert.IsTrue(report.EvidenceFiles.Any(x => x.File == "bok-ecos-stats.provider.events.jsonl" && x.Sha256.Length == 64));
        Assert.Contains("malicious producer", report.VerificationBoundary);
        Assert.IsTrue(report.EvidenceFiles.All(x => !Path.IsPathFullyQualified(x.File)));
        // The fixture invents observer records to exercise the offline parser. It does not prove
        // an actual process or token existed; only the live independent observer can supply that.
    }

    [TestMethod]
    public void LegacyStorageBundleRemainsReadableOnlyAsExplicitlyHistoricalEvidence()
    {
        using var fixture = new EvidenceFixture("TableCloth");

        var report = EvidenceVerifier.Verify(fixture.Root, EvidenceFixture.ProductHash);

        Assert.IsTrue(report.Passed, string.Join(",", report.Failures));
        Assert.AreEqual("legacy-installation-root", report.RuntimeStorageLayout);
        Assert.IsTrue(report.HistoricalStorageLayout);
        Assert.Contains("Historical offline evidence", report.RuntimeStorageBoundary);
    }

    [TestMethod]
    [DataRow("OtherApplication")]
    [DataRow("FakeTableCloth")]
    [DataRow("FakeTableCloth.Data")]
    public void UnrelatedOrSuffixOnlyApplicationDirectoriesAreRejected(string applicationDirectory)
    {
        using var fixture = new EvidenceFixture(applicationDirectory);

        var report = EvidenceVerifier.Verify(fixture.Root);

        Assert.IsFalse(report.Passed);
        Assert.AreEqual("unrecognized", report.RuntimeStorageLayout);
        Assert.Contains("owned-runtime-root", report.Failures);
    }

    [TestMethod]
    public void ModelClaimAndForgedSummaryWithoutMcpExecutionAreRejected()
    {
        using var fixture = new EvidenceFixture();
        fixture.Events("bok-ecos-stats", [new { type = "turn.started" }, new { type = "item.completed", item = new { id = "answer", type = "agent_message", text = "bok-ecos-stats 스킬을 사용했고 AppContainer에서 실행했습니다." } }, new { type = "turn.completed" }]);
        fixture.Write("summary.json", new { Passed = true, allExecutionChecksPassed = true });
        fixture.Write("bok-ecos-stats.chat.json", new { proof = new { Passed = true, SuccessfulContainedCalls = 1 } });

        Assert.IsFalse(EvidenceVerifier.Verify(fixture.Root).Passed);
    }

    [TestMethod]
    public void FabricatedMcpEnvelopeWithoutIndependentProcessRecordsIsRejected()
    {
        using var fixture = new EvidenceFixture();
        fixture.Write("bok-ecos-stats.processes.json", Array.Empty<object>());

        var report = EvidenceVerifier.Verify(fixture.Root);

        Assert.IsFalse(report.Passed);
        Assert.Contains("bok-ecos-stats:independent-worker-token", report.Failures);
    }

    [TestMethod]
    [DataRow("false-token")]
    [DataRow("unknown-token")]
    [DataRow("query-failed")]
    [DataRow("query-error")]
    [DataRow("wrong-parent")]
    [DataRow("wrong-image")]
    [DataRow("wrong-worker-hash")]
    public void InvalidKernelObservationCannotBeReplacedByTheJsonWasAppContainerFlag(string mutation)
    {
        using var fixture = new EvidenceFixture();
        var observations = JsonNode.Parse(File.ReadAllText(fixture.PathFor("bok-ecos-stats.processes.json")))!.AsArray();
        var worker = observations[1]!;
        switch (mutation)
        {
            case "false-token": worker["IsAppContainer"] = false; break;
            case "unknown-token": worker["IsAppContainer"] = null; break;
            case "query-failed": worker["TokenQuerySucceeded"] = false; break;
            case "query-error": worker["QueryError"] = "access-denied"; break;
            case "wrong-parent": worker["ParentProcessId"] = 9999; break;
            case "wrong-image": worker["ImagePath"] = fixture.ProductPath; break;
            default: worker["Sha256"] = new string('d', 64); break;
        }
        File.WriteAllText(fixture.PathFor("bok-ecos-stats.processes.json"), observations.ToJsonString());

        Assert.IsFalse(EvidenceVerifier.Verify(fixture.Root).Passed);
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("{\"error\":\"upstream_not_configured\"}")]
    [DataRow("[]")]
    [DataRow("[{\"ORG_ID\":null,\"TBL_ID\":\"DT_TEST\"}]")]
    [DataRow("[{\"ORG_ID\":\"101\",\"TBL_ID\":\"\"}]")]
    [DataRow("[{\"ORG_ID\":\"0\",\"TBL_ID\":\"DT_TEST\"}]")]
    [DataRow("[{\"ORG_ID\":101,\"TBL_ID\":\"DT_TEST\",\"error\":\"bad\"}]")]
    public void HttpSuccessEnvelopeWithInvalidKosisDataDoesNotProveACompletedSearch(string stdout)
    {
        using var fixture = new EvidenceFixture();
        fixture.ScriptEvents("kosis-stats", stdout);

        Assert.IsFalse(EvidenceVerifier.IsValidKosisSearchData(stdout));
        Assert.IsFalse(EvidenceVerifier.Verify(fixture.Root).Passed);
    }

    [TestMethod]
    [DataRow("[{\"ORG_ID\":\"101\",\"TBL_ID\":\"DT_1BPA002\"}]")]
    [DataRow("[{\"ORG_ID\":101,\"TBL_ID\":\"DT_1BPA002\"}]")]
    public void KosisOneRowAcceptsPositiveStringOrIntegerOrganizationIdentifiers(string stdout)
        => Assert.IsTrue(EvidenceVerifier.IsValidKosisSearchData(stdout));

    [TestMethod]
    public void WorkerExitFailureCannotBeOverriddenByValidLookingStdout()
    {
        using var fixture = new EvidenceFixture();
        fixture.ScriptEvents("tablecloth-calculators", EvidenceFixture.CalculatorOutput, exitCode: 1);

        Assert.IsFalse(EvidenceVerifier.Verify(fixture.Root).Passed);
    }

    [TestMethod]
    [DataRow("id")]
    [DataRow("server")]
    [DataRow("tool")]
    [DataRow("arguments")]
    [DataRow("error")]
    public void StartedAndCompletedMcpEventsMustMatchExactly(string mutation)
    {
        using var fixture = new EvidenceFixture();
        var lines = File.ReadAllLines(fixture.PathFor("tablecloth-calculators.provider.events.jsonl"));
        var completed = JsonNode.Parse(lines[2])!;
        var item = completed["item"]!;
        switch (mutation)
        {
            case "id": item["id"] = "unpaired-call"; break;
            case "server": item["server"] = "another-server"; break;
            case "tool": item["tool"] = "another-tool"; break;
            case "arguments": item["arguments"]!["stdin"] = "{\"operation\":\"bmi\",\"weight_kg\":81,\"height_cm\":171}"; break;
            default: item["error"] = "tool failed"; break;
        }
        lines[2] = completed.ToJsonString();
        File.WriteAllLines(fixture.PathFor("tablecloth-calculators.provider.events.jsonl"), lines);

        Assert.IsFalse(EvidenceVerifier.Verify(fixture.Root).Passed);
    }

    [TestMethod]
    public void ExternalProductHashAnchorRejectsAConsistentButDifferentRecordedHost()
    {
        using var fixture = new EvidenceFixture();

        var report = EvidenceVerifier.Verify(fixture.Root, new string('f', 64));

        Assert.IsFalse(report.Passed);
        Assert.Contains("product-hash-anchor", report.Failures);
    }

    [TestMethod]
    public void RawMutationChangesTheDigestAndBreaksItsRuntimeHashCorroboration()
    {
        using var fixture = new EvidenceFixture();
        var before = EvidenceVerifier.Verify(fixture.Root);
        var path = fixture.PathFor("runtime-seed-manifest.json");
        var seed = JsonNode.Parse(File.ReadAllText(path))!;
        seed["pythonSha256"] = new string('e', 64);
        File.WriteAllText(path, seed.ToJsonString());

        var after = EvidenceVerifier.Verify(fixture.Root);

        Assert.IsFalse(after.Passed);
        Assert.AreNotEqual(before.EvidenceFiles.Single(x => x.File == "runtime-seed-manifest.json").Sha256,
            after.EvidenceFiles.Single(x => x.File == "runtime-seed-manifest.json").Sha256);
    }

    private sealed class EvidenceFixture : IDisposable
    {
        internal const string ProductHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string PythonHash = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string NodeHash = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
        internal const string CalculatorOutput = "{\"ok\":true,\"operation\":\"bmi\",\"result\":{\"bmi\":\"27.36\"}}";
        private static readonly string[] Ids = ["bok-ecos-stats", "kosis-stats", "official-report-skillset", "tablecloth-certificate-expiry", "tablecloth-windows-sandbox", "tablecloth-calculators"];
        internal string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TableClothEvidenceSynthetic-" + Guid.NewGuid().ToString("N"));
        internal string ProductPath => System.IO.Path.Combine(Root, "synthetic-product", "TableCloth.exe");
        private readonly string _applicationDirectory;
        private string Owned => System.IO.Path.Combine(Root, "synthetic-local-appdata", _applicationDirectory, "ManagedAi", "skill-execution");
        private string Python => System.IO.Path.Combine(Owned, "runtimes", "py-synthetic", "python.exe");
        private string Node => System.IO.Path.Combine(Owned, "runtimes", "node-synthetic", "node.exe");

        internal EvidenceFixture(string applicationDirectory = "TableCloth.Data")
        {
            _applicationDirectory = applicationDirectory;
            Directory.CreateDirectory(Root);
            Write("runtime-seed-manifest.json", new { ownedRoot = Owned, pythonExecutable = Python, pythonSha256 = PythonHash, nodeExecutable = Node, nodeSha256 = NodeHash,
                runtimeFiles = new[] { new { path = Python, sha256 = PythonHash }, new { path = Node, sha256 = NodeHash } } });
            var appRoot = System.IO.Path.GetDirectoryName(Owned)!;
            var log = new List<object>
            {
                Stage("product-host", new { executable = ProductPath, sha256 = ProductHash }),
                Stage("codex-runtime", new { installed = true, executable = System.IO.Path.Combine(appRoot,"runtimes","openai-codex","release","codex.exe"), profile = System.IO.Path.Combine(appRoot,"profiles","openai-codex","codex-home") }),
                Stage("profile-policy", new { shellToolEnabled = false, unifiedExecEnabled = false, actualConfigContainsDisabledFeatures = true })
            };
            foreach (var id in Ids)
            {
                log.Add(new { stage = "chat-request", timestampUtc = "2026-10-27T00:00:01Z", details = new { id, realPrivateCertificateScan = false, realWindowsSandboxOperation = false, certificateReportIsSynthetic = id == "tablecloth-certificate-expiry", sandboxReportIsSynthetic = id == "tablecloth-windows-sandbox" } });
                log.Add(new { stage = "chat-result", timestampUtc = "2026-10-27T00:00:10Z", details = new { id, Passed = false } }); // Flags are deliberately untrusted.
                if (id is "bok-ecos-stats" or "kosis-stats" or "tablecloth-calculators")
                {
                    ScriptEvents(id, id switch { "bok-ecos-stats" => "[환율] 원/달러 환율(종가): 1323.1 원 (20261026)\r\n", "kosis-stats" => "[{\"ORG_ID\":\"101\",\"TBL_ID\":\"DT_TEST\"}]", _ => CalculatorOutput });
                    Write(id + ".processes.json", new object[]
                    {
                        new { Kind="broker",ProcessId=100,ParentProcessId=50,StartedUtc="2026-10-27T00:00:02Z",ObservedUtc="2026-10-27T00:00:03Z",ImagePath=ProductPath,Sha256=ProductHash,TokenQuerySucceeded=true,IsAppContainer=false,QueryError=(string?)null },
                        new { Kind="worker",ProcessId=101,ParentProcessId=100,StartedUtc="2026-10-27T00:00:04Z",ObservedUtc="2026-10-27T00:00:05Z",ImagePath=Python,Sha256=PythonHash,TokenQuerySucceeded=true,IsAppContainer=true,QueryError=(string?)null }
                    });
                }
                else
                {
                    var text = id switch { "official-report-skillset" => "추진 배경\n추진 경과\n주요 내용\n향후 추진 계획\n붙임", "tablecloth-certificate-expiry" => "합성 보고서: 만료 1건, 30일 이내 만료 2건입니다.", _ => "합성 보고서에 Windows Sandbox 세션이 0개입니다." };
                    Events(id, [new { type="turn.started" }, new { type="item.completed",item=new { id="answer",type="agent_message",text } },new { type="turn.completed" }]);
                    Write(id + ".processes.json", Array.Empty<object>());
                }
            }
            log.Add(Stage("completed", new { passed = false }));
            File.WriteAllLines(PathFor("probe.jsonl"), log.Select(item => JsonSerializer.Serialize(item)));
        }

        internal string PathFor(string name) => System.IO.Path.Combine(Root, name);
        internal void Write(string name, object value) => File.WriteAllText(PathFor(name), JsonSerializer.Serialize(value));
        internal void Events(string id, object[] entries) => File.WriteAllLines(PathFor(id + ".provider.events.jsonl"), entries.Select(value => JsonSerializer.Serialize(value)));
        internal void ScriptEvents(string id, string stdout, int exitCode = 0)
        {
            object arguments = id switch
            {
                "bok-ecos-stats" => new { arguments = new[] { "key", "--limit", "1", "--timeout", "15", "--text" } },
                "kosis-stats" => new { arguments = new[] { "search", "--query", "인구", "--result-count", "1", "--json" } },
                _ => new { stdin = "{\"operation\":\"bmi\",\"height_cm\":171,\"weight_kg\":80}" }
            };
            var payload = JsonSerializer.Serialize(new { status = exitCode == 0 ? "completed" : "failed", exitCode, stdout, stderr = "", wasAppContainer = true, timedOut = false, outputLimited = false, failure = (string?)null });
            Events(id, [
                new { type="turn.started" },
                new { type="item.started",item=new { id="call",type="mcp_tool_call",server="tablecloth_skills",tool=id,arguments,status="in_progress",error=(string?)null,result=(object?)null } },
                new { type="item.completed",item=new { id="call",type="mcp_tool_call",server="tablecloth_skills",tool=id,arguments,status="completed",error=(string?)null,result=new { content=new[] { new { type="text",text=payload } } } } },
                new { type="turn.completed" }
            ]);
        }
        private static object Stage(string stage, object details) => new { stage, timestampUtc = "2026-10-27T00:00:00Z", details };
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
    }
}
