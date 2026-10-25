using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TableCloth.AppContainerPoc;

public sealed record BrokerSelfCheckResult(string Name, bool Passed, string? Failure = null);

/// <summary>Offline fixture checks only. Fake executor evidence is never native isolation evidence.</summary>
public static class BrokerSelfChecks
{
    public static async Task<IReadOnlyList<BrokerSelfCheckResult>> RunAsync(string ownedRoot, CancellationToken token)
    {
        ownedRoot = Path.GetFullPath(ownedRoot);
        BrokerFiles.RejectReparsePoints(ownedRoot);
        var root = Path.Combine(ownedRoot, "broker-selfcheck-" + Guid.NewGuid().ToString("N"));
        BrokerFiles.RequireUnder(ownedRoot, root);
        Directory.CreateDirectory(Path.Combine(root, "runtime"));
        Directory.CreateDirectory(Path.Combine(root, "scripts"));
        var python = Path.Combine(root, "runtime", "python.exe");
        var node = Path.Combine(root, "runtime", "node.exe");
        var script = Path.Combine(root, "scripts", "fixture.py");
        var manifest = Path.Combine(root, "manifest.json");
        await File.WriteAllTextAsync(python, "offline fixture; never executed", token);
        await File.WriteAllTextAsync(node, "offline Node fixture; never executed", token);
        await File.WriteAllTextAsync(script, "# offline fixture; never executed\n", token);
        var original = await File.ReadAllBytesAsync(script, token);
        var checks = new List<BrokerSelfCheckResult>();
        var calls = 0;
        SandboxRunRequest? captured = null;
        Task<SandboxRunResult> Fake(SandboxRunRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); calls++; captured = request;
            return Task.FromResult(new SandboxRunResult(0, "{\"ok\":true,\"result\":{\"final\":\"80.00\"}}", "", false, false, true));
        }
        async Task Check(string name, Func<Task> action)
        {
            token.ThrowIfCancellationRequested();
            try { await action(); checks.Add(new(name, true)); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { checks.Add(new(name, false, "fixture assertion failed")); }
        }
        async Task<SkillExecutionRegistry> Registry(bool network = false, string kind = "python-json-stdin")
        {
            await WriteManifest(manifest, root, python, script, network: network, kind: kind, token: token);
            return await SkillExecutionRegistry.LoadAsync(manifest, Fake, token);
        }
        using var valid = JsonDocument.Parse("{\"operation\":\"discount.single\",\"price\":100,\"rate_percent\":20}");
        try
        {
            var registry = await Registry();
            await Check("fixed-worker-request-and-cleanup", async () =>
            {
                var result = await registry.ExecuteAsync("fixture", valid.RootElement, token);
                Require(!result.IsError && result.WasAppContainer && calls == 1);
                Require(captured is not null && !captured.AllowNetwork && captured.Executable == python);
                Require(captured!.Arguments.SequenceEqual(new[] { "-I", "-S", "-B", "-X", "utf8", script }));
                Require(captured.ReadOnlyDirectories.Count == 2 && captured.ReadWriteDirectories.SequenceEqual(new[] { captured.WorkingDirectory }));
                Require(!Directory.Exists(captured.WorkingDirectory));
            });
            await Check("tool-input-cannot-add-command-code-or-path", async () =>
            {
                var before = calls;
                foreach (var extra in new[] { "command", "code", "scriptPath", "executable", "arguments" })
                {
                    using var input = JsonDocument.Parse("{\"operation\":\"discount.single\",\"price\":100,\"rate_percent\":20,\"" + extra + "\":\"untrusted\"}");
                    Require((await registry.ExecuteAsync("fixture", input.RootElement, token)).Failure == "invalid-input");
                }
                using var expression = JsonDocument.Parse("{\"operation\":\"eval\",\"code\":\"1+1\"}");
                Require((await registry.ExecuteAsync("fixture", expression.RootElement, token)).IsError && calls == before);
            });
            await Check("duplicate-input-properties", async () =>
            {
                var before = calls;
                using var input = JsonDocument.Parse("{\"operation\":\"discount.single\",\"price\":100,\"price\":0,\"rate_percent\":20}");
                Require((await registry.ExecuteAsync("fixture", input.RootElement, token)).Failure == "invalid-input" && calls == before);
            });
            await Check("oversized-tool-input", async () =>
            {
                var before = calls;
                using var input = JsonDocument.Parse("{\"operation\":\"discount.single\",\"price\":\"" + new string('1', 65536) + "\",\"rate_percent\":20}");
                Require((await registry.ExecuteAsync("fixture", input.RootElement, token)).IsError && calls == before);
            });
            await Check("unknown-skill-never-executes", async () =>
            {
                var before = calls;
                Require((await registry.ExecuteAsync("../other", valid.RootElement, token)).Failure == "unknown-skill" && calls == before);
            });
            await Check("network-registration-never-executes", async () =>
            {
                var blocked = await Registry(network: true, kind: "python-cli-fixed");
                using var empty = JsonDocument.Parse("{}");
                var before = calls;
                Require((await blocked.ExecuteAsync("fixture", empty.RootElement, token)).Failure == "network-policy" && calls == before);
            });
            await Check("fixed-cli-has-no-model-arguments", async () =>
            {
                var fixedCli = await Registry(kind: "python-cli-fixed");
                using var empty = JsonDocument.Parse("{}");
                Require(!(await fixedCli.ExecuteAsync("fixture", empty.RootElement, token)).IsError);
                Require(captured!.Arguments.SequenceEqual(new[] { "-I", "-S", "-B", "-X", "utf8", script, "search", "--dry-run" }));
                var before = calls;
                Require((await fixedCli.ExecuteAsync("fixture", valid.RootElement, token)).IsError && calls == before);
            });
            await Check("node-fixture-has-fixed-main-option-and-numeric-input", async () =>
            {
                var nodeRegistry = await Registry(kind: "node-json-stdin");
                using var input = JsonDocument.Parse("{\"a\":1,\"b\":2}");
                Require(!(await nodeRegistry.ExecuteAsync("fixture", input.RootElement, token)).IsError);
                Require(captured!.Executable == node && !captured.AllowNetwork);
                Require(captured.Arguments.SequenceEqual(new[] { "--preserve-symlinks-main", script }));
                var before = calls;
                using var injected = JsonDocument.Parse("{\"a\":1,\"b\":2,\"arguments\":[\"--eval\",\"untrusted\"]}");
                Require((await nodeRegistry.ExecuteAsync("fixture", injected.RootElement, token)).IsError && calls == before);
            });
            await Check("changed-script-hash-is-rechecked", async () =>
            {
                var verified = await Registry(); var before = calls;
                await File.AppendAllTextAsync(script, "# changed\n", token);
                try { Require((await verified.ExecuteAsync("fixture", valid.RootElement, token)).Failure == "registration-integrity" && calls == before); }
                finally { await File.WriteAllBytesAsync(script, original, token); }
            });
            await Check("manifest-rejects-outside-script-path", async () =>
            {
                await WriteManifest(manifest, root, python, script, scriptPath: "../outside.py", token: token);
                await MustReject(() => SkillExecutionRegistry.LoadAsync(manifest, Fake, token));
            });
            await Check("manifest-rejects-outside-interpreter", async () =>
            {
                await WriteManifest(manifest, root, python, script, pythonPath: "../outside.exe", token: token);
                await MustReject(() => SkillExecutionRegistry.LoadAsync(manifest, Fake, token));
            });
            await Check("alternate-data-stream-paths-rejected-before-io", async () =>
            {
                // Normal drive colons remain valid. Do not create, open, or probe an actual ADS.
                BrokerFiles.RequireUnder(root, script);
                BrokerFiles.RequireUnder(root, python);
                foreach (var path in new[] { script + ":payload", script + ":payload:$DATA", python + ":payload" })
                {
                    var rejected = false;
                    try { BrokerFiles.RequireUnder(root, path); }
                    catch (BrokerValidationException) { rejected = true; }
                    Require(rejected);
                }
                await WriteManifest(manifest, root, python, script, scriptPath: "scripts/fixture.py:payload", token: token);
                await MustReject(() => SkillExecutionRegistry.LoadAsync(manifest, Fake, token));
            });
            await Check("manifest-rejects-incorrect-script-hash", async () =>
            {
                await WriteManifest(manifest, root, python, script, hash: new string('0', 64), token: token);
                await MustReject(() => SkillExecutionRegistry.LoadAsync(manifest, Fake, token));
            });
            await Check("manifest-rejects-duplicate-properties", async () =>
            {
                await WriteManifest(manifest, root, python, script, token: token);
                var text = await File.ReadAllTextAsync(manifest, token);
                await File.WriteAllTextAsync(manifest, "{\"ownedRoot\":\"duplicate\"," + text[1..], token);
                await MustReject(() => SkillExecutionRegistry.LoadAsync(manifest, Fake, token));
            });
            await Check("worker-requires-appcontainer-evidence", async () =>
            {
                await WriteManifest(manifest, root, python, script, token: token);
                var outside = await SkillExecutionRegistry.LoadAsync(manifest,
                    (_, _) => Task.FromResult(new SandboxRunResult(0, "not trusted", "", false, false, false)), token);
                var result = await outside.ExecuteAsync("fixture", valid.RootElement, token);
                Require(result.Failure == "appcontainer-required" && result.Stdout == "");
            });
            await Check("worker-output-is-bounded", async () =>
            {
                var large = await SkillExecutionRegistry.LoadAsync(manifest,
                    (_, _) => Task.FromResult(new SandboxRunResult(0, new string('x', 65537), "", false, false, true)), token);
                var result = await large.ExecuteAsync("fixture", valid.RootElement, token);
                Require(result.OutputLimited && result.Stdout == "" && result.IsError);
            });
            registry = await Registry();
            await Check("stdio-jsonrpc-and-tool-contract", async () =>
            {
                var requests = string.Join('\n',
                    "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"offline-fixture\",\"version\":\"1\"}}}",
                    "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
                    "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"ping\"}",
                    "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/list\"}",
                    "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/call\",\"params\":{\"name\":\"fixture\",\"arguments\":{\"operation\":\"discount.single\",\"price\":100,\"rate_percent\":20}}}",
                    "{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"tools/call\",\"params\":{\"name\":\"unknown\"}}",
                    "{\"jsonrpc\":\"2.0\",\"id\":6,\"method\":\"tools/call\",\"params\":{\"name\":\"fixture\",\"arguments\":{\"operation\":\"exec\",\"code\":\"untrusted\"}}}",
                    "{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"unimplemented\"}",
                    "{\"jsonrpc\":\"2.0\",\"id\":8,\"id\":9,\"method\":\"ping\"}",
                    "{\"jsonrpc\":\"2.0\",\"id\":10,\"method\":\"tools/call\",\"params\":{\"name\":\"fixture\",\"arguments\":[]}}",
                    "{\"jsonrpc\":\"2.0\",\"method\":\"tools/call\",\"params\":{\"name\":\"fixture\"}}") + "\n";
                var before = calls;
                var (exit, responses) = await Exchange(registry, Encoding.UTF8.GetBytes(requests), token);
                Require(exit == 0 && responses.Count == 9 && calls == before + 1);
                foreach (var response in responses) Require(response.GetProperty("jsonrpc").GetString() == "2.0");
                var tools = responses[2].GetProperty("result").GetProperty("tools");
                Require(tools.GetArrayLength() == 1 && tools[0].GetProperty("inputSchema").GetProperty("properties").TryGetProperty("price", out _));
                Require(!responses[3].GetProperty("result").GetProperty("isError").GetBoolean());
                Require(responses[4].GetProperty("error").GetProperty("code").GetInt32() == -32602);
                Require(responses[5].GetProperty("result").GetProperty("isError").GetBoolean());
                Require(responses[7].GetProperty("id").ValueKind == JsonValueKind.Null);
            });
            await Check("stdio-oversize-ends-with-protocol-error", async () =>
            {
                var before = calls;
                var (exit, responses) = await Exchange(registry, Encoding.UTF8.GetBytes(new string('x', 131073) + "\n"), token);
                Require(exit == 2 && responses.Count == 1 && responses[0].GetProperty("error").GetProperty("code").GetInt32() == -32700 && calls == before);
            });
            await Check("stdio-invalid-utf8-has-no-content-leak", async () =>
            {
                var (exit, responses) = await Exchange(registry, new byte[] { 0xff, (byte)'\n' }, token);
                Require(exit == 2 && responses.Single().GetProperty("error").GetProperty("code").GetInt32() == -32700);
            });
            await Check("cancellation-awaits-fake-worker-and-cleans-work", async () =>
            {
                var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var stopped = false;
                string? runningWork = null;
                var pendingRegistry = await SkillExecutionRegistry.LoadAsync(manifest, async (request, ct) =>
                {
                    runningWork = request.WorkingDirectory; started.SetResult();
                    try { await Task.Delay(Timeout.Infinite, ct); }
                    finally { await Task.Yield(); stopped = true; }
                    return new SandboxRunResult(0, "", "", false, false, true);
                }, token);
                using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
                var pending = pendingRegistry.ExecuteAsync("fixture", valid.RootElement, stop.Token);
                await started.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
                await stop.CancelAsync();
                try { await pending; throw new InvalidOperationException(); }
                catch (OperationCanceledException) { Require(stopped && runningWork is not null && !Directory.Exists(runningWork)); }
            });
        }
        finally
        {
            BrokerFiles.RequireUnder(ownedRoot, root);
            BrokerFiles.RejectReparsePoints(root);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
        return checks.AsReadOnly();
    }

    private static async Task<(int ExitCode, List<JsonElement> Responses)> Exchange(SkillExecutionRegistry registry, byte[] bytes, CancellationToken token)
    {
        using var input = new MemoryStream(bytes); using var output = new MemoryStream();
        var exit = await SkillMcpBroker.RunAsync(registry, input, output, token);
        var responses = new List<JsonElement>();
        foreach (var line in Encoding.UTF8.GetString(output.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        { using var json = JsonDocument.Parse(line); responses.Add(json.RootElement.Clone()); }
        return (exit, responses);
    }

    private static async Task WriteManifest(string path, string root, string python, string script,
        string? scriptPath = null, string? pythonPath = null, string? hash = null, bool network = false,
        string kind = "python-json-stdin", CancellationToken token = default)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject(); writer.WriteString("ownedRoot", root);
            writer.WriteString("pythonExecutable", pythonPath ?? "runtime/python.exe");
            writer.WriteString("pythonSha256", Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(python, token))));
            if (kind == "node-json-stdin")
            {
                writer.WriteString("nodeExecutable", "runtime/node.exe");
                writer.WriteString("nodeSha256", Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(root, "runtime", "node.exe"), token))));
            }
            writer.WritePropertyName("skills"); writer.WriteStartArray(); writer.WriteStartObject();
            writer.WriteString("skillId", "fixture"); writer.WriteString("kind", kind); writer.WriteString("scriptPath", scriptPath ?? "scripts/fixture.py");
            writer.WriteString("sha256", hash ?? Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(script, token))));
            writer.WriteBoolean("requiresNetwork", network);
            if (kind == "python-cli-fixed")
            {
                writer.WritePropertyName("fixedArguments"); writer.WriteStartArray(); writer.WriteStringValue("search"); writer.WriteStringValue("--dry-run"); writer.WriteEndArray();
            }
            writer.WriteEndObject(); writer.WriteEndArray(); writer.WriteEndObject();
        }
        await File.WriteAllBytesAsync(path, stream.ToArray(), token);
    }

    private static async Task MustReject(Func<Task<SkillExecutionRegistry>> action)
    {
        try { await action(); }
        catch (Exception error) when (error is BrokerValidationException or JsonException) { return; }
        throw new InvalidOperationException();
    }

    private static void Require(bool condition) { if (!condition) throw new InvalidOperationException(); }
}
