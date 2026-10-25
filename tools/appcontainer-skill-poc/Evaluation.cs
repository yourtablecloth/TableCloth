using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace TableCloth.AppContainerPoc;

internal static class Evaluation
{
    private sealed record Check(string Name, bool Passed, object? Evidence);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    internal static async Task<int> RunAsync(string root, bool includePublicApi, CancellationToken token)
    {
        root = Path.GetFullPath(root);
        Preparation.RejectReparse(root);
        var manifest = Preparation.Under(root, "evaluation-manifest.json");
        var registry = await SkillExecutionRegistry.LoadAsync(manifest, token);
        var runner = new AppContainerProcessRunner(root);
        var checks = new List<Check>();
        async Task CheckAsync(string name, Func<Task<(bool, object?)>> run)
        {
            try
            {
                var (passed, evidence) = await run();
                checks.Add(new(name, passed, evidence));
                Console.WriteLine((passed ? "PASS " : "FAIL ") + name);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                checks.Add(new(name, false, exception.GetType().Name + ": " + exception.Message));
                Console.WriteLine("FAIL " + name + ": " + exception.GetType().Name);
            }
        }
        async Task<(bool, object?)> Calculate(string json, Func<JsonElement, bool> expected)
        {
            var result = await registry.ExecuteAsync("tablecloth-calculators", Parse(json), token);
            var passed = result.Status == "completed" && result.WasAppContainer && expected(Parse(result.Stdout).GetProperty("result"));
            return (passed, result);
        }

        foreach (var check in await BrokerSelfChecks.RunAsync(root, token))
        {
            checks.Add(new("broker-offline." + check.Name, check.Passed, check.Failure));
            Console.WriteLine((check.Passed ? "PASS " : "FAIL ") + "broker-offline." + check.Name);
        }

        var aclPaths = new[] { root, Path.Combine(root, "runtime/python"), Path.Combine(root, "runtime/python/python.exe"),
            Path.Combine(root, "runtime/node/node.exe"), Path.Combine(root, "skills/tablecloth-calculators/scripts/calculate.py") };
        var originalAcls = aclPaths.ToDictionary(x => x, ReadDacl);
        await CheckAsync("calculator.discount", () => Calculate("""{"operation":"discount.single","price":"100","rate_percent":"20"}""", r => r.GetProperty("final").GetString() == "80.00"));
        await CheckAsync("calculator.percent", () => Calculate("""{"operation":"percent.part","total":"200","rate_percent":"12.5"}""", r => r.GetProperty("value").GetString() == "25"));
        await CheckAsync("calculator.leap-date", () => Calculate("""{"operation":"date.add","date":"2024-02-29","years":1}""", r => r.GetProperty("date").GetString() == "2025-02-28"));
        await CheckAsync("calculator.time-rollover", () => Calculate("""{"operation":"time.convert","datetime":"2024-01-01T01:00","from_offset":"+09:00","to_offset":"-05:00"}""", r => r.GetProperty("converted").GetString()!.StartsWith("2023-12-31T11:00", StringComparison.Ordinal)));
        await CheckAsync("calculator.bmi", () => Calculate("""{"operation":"bmi","weight_kg":"70","height_cm":"175"}""", r => r.GetProperty("bmi").GetString() == "22.86"));
        await CheckAsync("calculator.zero-rate-loan", () => Calculate("""{"operation":"loan","principal":"100","annual_rate_percent":"0","periods":3,"periods_per_year":12,"method":"annuity","schedule":"full"}""", r => r.GetProperty("schedule")[2].GetProperty("closing_balance").GetString() == "0.00"));
        await CheckAsync("kosis.fixed-dry-run", async () =>
        {
            var result = await registry.ExecuteAsync("kosis-stats", Parse("{}"), token);
            return (result.Status == "completed" && result.WasAppContainer && Parse(result.Stdout).GetProperty("via_proxy").GetBoolean(), result);
        });
        await CheckAsync("bok.broker-network-denied", async () =>
        {
            var result = await registry.ExecuteAsync("bok-ecos-stats", Parse("{}"), token);
            return (result.Status == "blocked" && result.Failure == "network-policy", result);
        });
        await CheckAsync("node.registered-script", async () =>
        {
            var result = await registry.ExecuteAsync("node-runtime-fixture", Parse("""{"a":12,"b":30}"""), token);
            return (result.Status == "completed" && result.WasAppContainer && Parse(result.Stdout).GetProperty("sum").GetInt32() == 42, result);
        });
        foreach (var skill in new[] { "tablecloth-calculators", "bok-ecos-stats" })
        {
            await CheckAsync(skill + ".upstream-unit-tests-contained", async () =>
            {
                var work = Preparation.Under(root, "probe-work/" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(work);
                var python = Preparation.Under(root, "runtime/python/python.exe");
                var source = Preparation.Under(root, "skills/" + skill);
                var result = await runner.RunAsync(new(python, ["-I", "-S", "-B", "-X", "utf8", "-m", "unittest", "discover", "-s", Path.Combine(source, "tests")],
                    work, "", [Path.GetDirectoryName(python)!, source], [work]), token);
                return (result.ExitCode == 0 && result.WasAppContainer && result.Failure is null && result.Stderr.Contains("\nOK", StringComparison.Ordinal), result);
            });
        }
        await CheckAsync("broker.no-arbitrary-code", async () =>
        {
            var result = await registry.ExecuteAsync("tablecloth-calculators", Parse("""{"operation":"eval","code":"print(1)"}"""), token);
            var args = await registry.ExecuteAsync("kosis-stats", Parse("""{"arguments":["--direct"]}"""), token);
            return (result.Status == "blocked" && args.Status == "blocked", new { result, args });
        });

        var privateDirectory = Preparation.Under(root, "private-canary");
        Directory.CreateDirectory(privateDirectory);
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(privateDirectory).SetAccessControl(security);
        var canary = Path.Combine(privateDirectory, "canary.txt");
        await File.WriteAllTextAsync(canary, "owned-synthetic-canary", token);
        await CheckAsync("probe.host-positive-control", async () =>
        {
            var value = await File.ReadAllTextAsync(canary, token);
            await File.WriteAllTextAsync(Path.Combine(privateDirectory, "host-write.txt"), "host", token);
            return (value == "owned-synthetic-canary", "Host can read and write the synthetic private directory.");
        });
        Environment.SetEnvironmentVariable("TABLECLOTH_POC_SYNTHETIC_SECRET", "synthetic-not-a-credential");
        try
        {
            foreach (var language in new[] { "python", "node" })
            {
                await CheckAsync(language + ".isolation", async () =>
                {
                    var input = JsonSerializer.Serialize(new { outside_read = canary, outside_write = Path.Combine(privateDirectory, language + "-write.txt") });
                    var result = await RunProbeAsync(root, runner, language, language == "python" ? "isolation.py" : "isolation.cjs", input, token);
                    var evidence = result.ExitCode == 0 ? Parse(result.Stdout) : default;
                    var passed = result.ExitCode == 0 && result.WasAppContainer && result.Failure is null &&
                        AccessDenied(evidence.GetProperty("outside_read")) &&
                        AccessDenied(evidence.GetProperty("outside_write")) &&
                        evidence.GetProperty("work_write").GetBoolean() && evidence.GetProperty("inherited_secret").ValueKind == JsonValueKind.Null &&
                        ProcessDenied(evidence.GetProperty("child")) &&
                        NetworkDenied(evidence.GetProperty("network"));
                    return (passed, result);
                });
            }
        }
        finally { Environment.SetEnvironmentVariable("TABLECLOTH_POC_SYNTHETIC_SECRET", null); }
        await CheckAsync("python.utf8-roundtrip", async () =>
        {
            var result = await RunProbeAsync(root, runner, "python", "unicode.py", "{\"text\":\"격리 확인\"}", token);
            return (result.ExitCode == 0 && result.WasAppContainer && result.Failure is null &&
                Parse(result.Stdout).GetProperty("text").GetString() == "격리 확인" &&
                Parse(result.Stdout).GetProperty("stdout_encoding").GetString() == "utf-8", result);
        });
        await CheckAsync("node.child-positive-control-two-process-job", async () =>
        {
            var input = JsonSerializer.Serialize(new { outside_read = canary, outside_write = Path.Combine(privateDirectory, "node-positive-write.txt") });
            var result = await RunProbeAsync(root, runner, "node", "isolation.cjs", input, token, maximumProcesses: 2);
            return (result.ExitCode == 0 && result.WasAppContainer && result.Failure is null &&
                Parse(result.Stdout).GetProperty("child").GetProperty("allowed").GetBoolean() &&
                Parse(result.Stdout).GetProperty("child").GetProperty("exit").GetInt32() == 0, result);
        });
        await CheckAsync("python.timeout", async () =>
        {
            var result = await RunProbeAsync(root, runner, "python", "wait.py", "", token, timeout: 700);
            return (result.TimedOut && result.WasAppContainer && result.Failure is null, result);
        });
        await CheckAsync("python.output-bound", async () =>
        {
            var result = await RunProbeAsync(root, runner, "python", "output.py", "", token, maximumOutput: 8192);
            return (result.OutputLimited && result.WasAppContainer && result.Failure is null && Encoding.UTF8.GetByteCount(result.Stdout + result.Stderr) <= 8192,
                result with { Stdout = "<bounded output omitted>" });
        });
        await CheckAsync("python.memory-bound", async () =>
        {
            var result = await RunProbeAsync(root, runner, "python", "memory.py", "", token, memoryLimit: 64L * 1024 * 1024);
            return (result.ExitCode == 0 && result.WasAppContainer && result.Failure is null && Parse(result.Stdout).GetProperty("memory_error").GetBoolean(), result);
        });
        await CheckAsync("python.cancellation", async () =>
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            cancellation.CancelAfter(700);
            try { await RunProbeAsync(root, runner, "python", "wait.py", "", cancellation.Token); return (false, "Cancellation was not observed."); }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { return (true, "Cancellation terminated the worker and awaited cleanup."); }
        });
        await CheckAsync("mcp.stdio-real-worker", () => McpAsync(manifest, token));
        await CheckAsync("native.acl-restored", () => Task.FromResult<(bool, object?)>((originalAcls.All(x => ReadDacl(x.Key) == x.Value), "Compared original and final DACLs for owned root, Python runtime, Node executable, and calculator script.")));
        await CheckAsync("native.workspace-cleaned", () => Task.FromResult<(bool, object?)>((
            !Directory.EnumerateDirectories(Preparation.Under(root, ".appcontainer-runs")).Any(), "No per-run native workspaces remain.")));

        if (includePublicApi)
        {
            foreach (var skill in new[] { "bok-ecos-stats", "kosis-stats" })
            {
                await CheckAsync(skill + ".public-api-evaluation", async () =>
                {
                    var work = Preparation.Under(root, "public-api-work/" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(work);
                    var script = Preparation.Under(root, "skills/" + skill + "/scripts/" + (skill == "bok-ecos-stats" ? "bok_ecos.py" : "run_kosis_stats.py"));
                    var arguments = new List<string> { "-I", "-S", "-B", "-X", "utf8", script };
                    if (skill == "bok-ecos-stats")
                    {
                        var secrets = Path.Combine(work, "empty-secrets.env");
                        await File.WriteAllTextAsync(secrets, "", token);
                        arguments.AddRange(["key", "--limit", "1", "--timeout", "15", "--secrets-path", secrets]);
                    }
                    else arguments.AddRange(["search", "--query", "인구", "--result-count", "1", "--json", "--timeout", "15"]);
                    var python = Preparation.Under(root, "runtime/python/python.exe");
                    var result = await runner.RunAsync(new(python, arguments, work, "", [Path.GetDirectoryName(python)!, Path.GetDirectoryName(script)!], [work],
                        AllowNetwork: true, TimeoutMilliseconds: 22000), token);
                    var validData = result.ExitCode == 0 && !result.Stdout.Contains('\uFFFD') && (skill == "bok-ecos-stats"
                        ? Parse(result.Stdout).GetProperty("result").GetString() == "ok" && Parse(result.Stdout).GetProperty("rows").GetArrayLength() == 1
                        : Parse(result.Stdout).ValueKind == JsonValueKind.Array && Parse(result.Stdout).GetArrayLength() == 1 &&
                          Parse(result.Stdout)[0].GetProperty("QUERY").GetString() == "인구");
                    return (result.WasAppContainer && validData && result.Failure is null,
                        new { capability = "internetClient, no domain allowlist; fixed host-owned evaluation only", sourceUnmodified = true, result });
                });
            }
        }
        var report = new { timestampUtc = DateTimeOffset.UtcNow, os = Environment.OSVersion.VersionString,
            architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
            shellToolEnabled = false, publicApiEvaluation = includePublicApi, passed = checks.Count(x => x.Passed), total = checks.Count, checks };
        var reportPath = Preparation.Under(root, includePublicApi ? "evaluation-public-api.json" : "evaluation.json");
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), token);
        Console.WriteLine($"{report.passed}/{report.total} checks passed. Evidence: {reportPath}");
        return checks.All(x => x.Passed) ? 0 : 1;
    }

    private static string ReadDacl(string path)
    {
        Win32.CheckError(Win32.GetNamedSecurityInfo(path, 1, Win32.SecurityInformation, out _, out _, out _, out _, out var descriptor));
        try
        {
            Win32.Check(Win32.DescriptorToString(descriptor, 1, Win32.SecurityInformation, out var text, out _));
            try
            {
                var security = new RawSecurityDescriptor(Marshal.PtrToStringUni(text)!);
                var labels = new List<string>();
                if (security.SystemAcl is not null)
                    foreach (GenericAce ace in security.SystemAcl)
                        if ((byte)ace.AceType == 17)
                        {
                            var binary = new byte[ace.BinaryLength]; ace.GetBinaryForm(binary, 0);
                            labels.Add(Convert.ToHexString(binary));
                        }
                // LABEL-only restoration can retain an empty SACL control bit. Compare actual label ACEs.
                return security.GetSddlForm(AccessControlSections.Access) + "|ML:" + string.Join(',', labels);
            }
            finally { Win32.LocalFree(text); }
        }
        finally { Win32.LocalFree(descriptor); }
    }

    private static JsonElement Parse(string value) { using var json = JsonDocument.Parse(value); return json.RootElement.Clone(); }

    private static bool AccessDenied(JsonElement value) => !value.GetProperty("allowed").GetBoolean() &&
        (value.TryGetProperty("winerror", out var winerror) && winerror.ValueKind == JsonValueKind.Number && winerror.GetInt32() is 5 or 1816 ||
         value.TryGetProperty("errno", out var errno) && errno.ValueKind == JsonValueKind.Number && errno.GetInt32() == 13 ||
         value.TryGetProperty("code", out var code) && code.GetString() is "EACCES" or "EPERM" or "EAGAIN");

    private static bool NetworkDenied(JsonElement value) => !value.GetProperty("allowed").GetBoolean() &&
        (value.TryGetProperty("winerror", out var winerror) && winerror.ValueKind == JsonValueKind.Number && winerror.GetInt32() == 10013 ||
         value.TryGetProperty("code", out var code) && code.GetString() is "EACCES" or "EPERM");

    private static bool ProcessDenied(JsonElement value) => AccessDenied(value) ||
        (!value.GetProperty("allowed").GetBoolean() && value.TryGetProperty("code", out var code) && code.GetString() == "UNKNOWN" &&
         value.TryGetProperty("errno", out var errno) && errno.GetInt32() == -4094);

    private static async Task<SandboxRunResult> RunProbeAsync(string root, AppContainerProcessRunner runner,
        string language, string probe, string input, CancellationToken token, int timeout = 10000, int maximumOutput = 65536, long memoryLimit = 268435456, int maximumProcesses = 1)
    {
        var work = Preparation.Under(root, "probe-work/" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var executable = Preparation.Under(root, language == "python" ? "runtime/python/python.exe" : "runtime/node/node.exe");
        var script = Preparation.Under(root, "probes/" + probe);
        var arguments = language == "python" ? new[] { "-I", "-S", "-B", "-X", "utf8", script } : new[] { "--preserve-symlinks-main", script };
        var original = ReadDacl(work);
        var result = await runner.RunAsync(new(executable, arguments, work, input,
            [Path.GetDirectoryName(executable)!, Path.GetDirectoryName(script)!], [work],
            TimeoutMilliseconds: timeout, MaxOutputBytes: maximumOutput, MemoryLimitBytes: memoryLimit, MaxProcesses: maximumProcesses), token);
        return original == ReadDacl(work) ? result : result with { Failure = "probe-work-dacl-or-label-not-restored" };
    }

    private static async Task<(bool, object?)> McpAsync(string manifest, CancellationToken token)
    {
        var host = Environment.ProcessPath!;
        var start = new ProcessStartInfo(host) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        if (Path.GetFileNameWithoutExtension(host).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(typeof(Evaluation).Assembly.Location);
        start.ArgumentList.Add("mcp"); start.ArgumentList.Add(manifest);
        using var process = Process.Start(start) ?? throw new IOException("Could not start MCP broker.");
        var output = process.StandardOutput.ReadToEndAsync(token);
        var error = process.StandardError.ReadToEndAsync(token);
        await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"poc-evaluation","version":"1"}}}""");
        await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""");
        await process.StandardInput.WriteLineAsync("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"tablecloth-calculators","arguments":{"operation":"discount.single","price":"100","rate_percent":"20"}}}""");
        process.StandardInput.Close();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(45000);
        try { await process.WaitForExitAsync(deadline.Token); }
        catch { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(CancellationToken.None); throw; }
        var lines = (await output).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(Parse).ToArray();
        var execution = lines.Length == 3 && lines[2].TryGetProperty("result", out var result)
            ? Parse(result.GetProperty("content")[0].GetProperty("text").GetString()!) : default;
        return (process.ExitCode == 0 && lines.Length == 3 && lines[1].GetProperty("result").GetProperty("tools").GetArrayLength() == 4 &&
            execution.GetProperty("status").GetString() == "completed" && execution.GetProperty("wasAppContainer").GetBoolean(),
            new { process.ExitCode, responses = lines, stderr = await error });
    }
}
