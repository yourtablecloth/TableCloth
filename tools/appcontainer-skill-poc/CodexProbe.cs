using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace TableCloth.AppContainerPoc;

/// <summary>Real pinned CLI/MCP routing with a deterministic local Responses stub, never model inference.</summary>
public static class CodexProbe
{
    private const string Tool = "mcp__tablecloth_skill_poc__tablecloth-calculators";
    private const string ToolNamespace = "mcp__tablecloth_skill_poc";
    private const string ToolFunction = "tablecloth_calculators";
    private const string Marker = "LOCAL_DETERMINISTIC_TOOL_ROUTING_OK";
    private const string Call = "call_tablecloth_local_calculator";
    private const string Input = """{"operation":"discount.single","price":"100","rate_percent":"20"}""";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static async Task<int> RunAsync(string root, CancellationToken token)
    {
        var workspace = FindWorkspace();
        root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var expected = Preparation.Under(workspace, "Artifacts/appcontainer-skill-poc");
        if (!root.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The probe requires its project-owned artifact root.");
        Preparation.RejectReparse(root);
        var manifest = Preparation.Under(root, "evaluation-manifest.json");
        if (!File.Exists(manifest)) throw new FileNotFoundException("Prepare the owned evaluation manifest first.");
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TableCloth", "ManagedAi", "runtimes", "openai-codex", "releases",
            "0.156.1-x86_64-pc-windows-msvc", "bin", "codex.exe");
        if (!File.Exists(executable)) throw new FileNotFoundException("Pinned Codex 0.156.1 is unavailable.");
        Preparation.RejectReparse(executable);
        var dotnet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
        if (!File.Exists(dotnet)) throw new FileNotFoundException("The absolute .NET host is unavailable.");
        var broker = typeof(CodexProbe).Assembly.Location;
        Preparation.RejectReparse(broker);
        var parent = Preparation.Under(root, "codex-home");
        Directory.CreateDirectory(parent);
        var home = Preparation.Under(parent, "probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(home);
        foreach (var folder in new[] { "user", "appdata", "localappdata", "temp", "dotnet-home" })
            Directory.CreateDirectory(Preparation.Under(home, folder));
        var config = Preparation.Under(home, "config.toml");
        var auth = Preparation.Under(home, "auth.json");
        var manifestHash = Preparation.Hash(manifest);
        var script = Preparation.Under(root, "skills/tablecloth-calculators/scripts/calculate.py");
        var scriptHash = Preparation.Hash(script);
        var port = FreeLoopbackPort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        var toml = $$"""
            model = "poc-local-deterministic"
            model_provider = "local_poc"
            approval_policy = "never"
            sandbox_mode = "read-only"
            cli_auth_credentials_store = "file"
            check_for_update_on_startup = false
            web_search = "disabled"
            feedback.enabled = false
            project_doc_max_bytes = 0
            [history]
            persistence = "none"
            [otel]
            exporter = "none"
            metrics_exporter = "none"
            trace_exporter = "none"
            log_user_prompt = false
            [features]
            shell_tool = false
            unified_exec = false
            shell_snapshot = false
            multi_agent = false
            hooks = false
            remote_plugin = false
            skill_mcp_dependency_install = false
            [model_providers.local_poc]
            name = "Local deterministic routing stub; no model inference"
            base_url = "http://127.0.0.1:{{port}}/v1"
            wire_api = "responses"
            requires_openai_auth = false
            request_max_retries = 0
            stream_max_retries = 0
            [mcp_servers.tablecloth_skill_poc]
            command = {{Toml(dotnet)}}
            args = [{{Toml(broker)}}, "mcp", {{Toml(manifest)}}]
            cwd = {{Toml(root)}}
            enabled_tools = ["tablecloth-calculators"]
            required = true
            startup_timeout_sec = 30
            tool_timeout_sec = 60
            default_tools_approval_mode = "prompt"
            [mcp_servers.tablecloth_skill_poc.tools."tablecloth-calculators"]
            approval_mode = "approve"
            [mcp_servers.tablecloth_skill_poc.env]
            DOTNET_GENERATE_ASPNET_CERTIFICATE = "false"
            DOTNET_CLI_TELEMETRY_OPTOUT = "1"
            DOTNET_CLI_HOME = {{Toml(Preparation.Under(home, "dotnet-home"))}}
            """;
        await File.WriteAllTextAsync(config, toml, new UTF8Encoding(false), token);
        var configHash = Preparation.Hash(config);
        var stub = new Stub();
        ProcessResult? version = null;
        ProcessResult? execution = null;
        string? failure = null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(100));
        Task? serving = null;
        try
        {
            listener.Start();
            serving = stub.ServeAsync(listener, deadline.Token);
            version = await RunProcessAsync(executable, ["--version"], "", root, home, deadline.Token);
            if (version.ExitCode != 0 || version.Stdout.Trim() != "codex-cli 0.156.1")
                throw new InvalidDataException("The executable did not report pinned Codex 0.156.1.");
            execution = await RunProcessAsync(executable,
                ["-c", "features.shell_tool=false", "-c", "features.unified_exec=false", "--ask-for-approval", "never",
                 "exec", "--json", "--sandbox", "read-only", "--skip-git-repo-check", "--ephemeral", "--color", "never", "-C", root, "-"],
                "Use the registered calculator MCP tool for the synthetic example: price 100, discount 20 percent. Return the verified tool result. This is a local routing test, not a real model request.", root, home, deadline.Token);
        }
        catch (OperationCanceledException) { failure = token.IsCancellationRequested ? "canceled" : "timeout"; }
        catch (Exception error) { failure = error.GetType().Name; }
        finally
        {
            deadline.Cancel();
            listener.Close();
            if (serving is not null)
                try { await serving; }
                catch (Exception error) when (error is OperationCanceledException or HttpListenerException or ObjectDisposedException) { }
        }
        var mcpEvent = execution is not null && HasCompletedMcpEvent(execution.Stdout);
        var finalEvent = execution is not null && HasFinalEvent(execution.Stdout);
        var unchanged = Preparation.Hash(config) == configHash && Preparation.Hash(manifest) == manifestHash && Preparation.Hash(script) == scriptHash;
        var noAuthFile = !File.Exists(auth);
        var passed = failure is null && stub.Failure is null && execution?.ExitCode == 0 && !execution.OutputLimited && stub.ResponseRequests == 2 &&
            stub.ToolWasAdvertised && !stub.ShellWasAdvertised && !stub.CredentialHeaderPresent &&
            stub.Execution is not null && mcpEvent && finalEvent && unchanged && noAuthFile;
        var evidence = new
        {
            recordedUtc = DateTimeOffset.UtcNow, success = passed,
            qualification = "Actual pinned Codex CLI -> MCP -> native AppContainer -> pinned calculator, driven by deterministic loopback Responses SSE. This does not test real model selection or inference.",
            modelInference = false, authenticationAttempted = false, existingAuthenticationCopied = false,
            configuredProvider = "loopback-only deterministic stub", shellTool = false, unifiedExec = false,
            approvalPolicy = "never", approvedMcpTool = "tablecloth_skill_poc/tablecloth-calculators",
            codexVersion = version?.Stdout.Trim(), configSha256 = configHash, manifestSha256 = manifestHash, calculatorSha256 = scriptHash,
            filesUnchanged = unchanged, isolatedAuthFileAbsent = noAuthFile,
            responseRequests = stub.ResponseRequests, advertisedTools = stub.AdvertisedTools,
            toolCallNamespace = stub.CallNamespace, toolCallName = stub.CallName,
            toolWasAdvertised = stub.ToolWasAdvertised, shellWasAdvertised = stub.ShellWasAdvertised,
            credentialHeaderPresent = stub.CredentialHeaderPresent, actualAppContainerResult = stub.Execution,
            completedMcpEvent = mcpEvent, finalAgentEvent = finalEvent, exitCode = execution?.ExitCode,
            observedMcpEventTool = execution is null ? null : McpEventToolName(execution.Stdout),
            failure, stubFailure = stub.Failure, processOutputLimited = execution?.OutputLimited ?? false,
            codexHomeRelative = Path.GetRelativePath(root, home)
        };
        await File.WriteAllTextAsync(Preparation.Under(root, "codex-probe-evidence.json"), JsonSerializer.Serialize(evidence, JsonOptions), CancellationToken.None);
        if (execution is not null)
        {
            await File.WriteAllTextAsync(Preparation.Under(home, "events.jsonl"), execution.Stdout, CancellationToken.None);
            await File.WriteAllTextAsync(Preparation.Under(home, "stderr.txt"), execution.Stderr, CancellationToken.None);
        }
        Console.WriteLine((passed ? "PASS" : "FAIL") + " pinned Codex deterministic local MCP routing; real model inference: 0");
        return passed ? 0 : 1;
    }

    private sealed record ProcessResult(int ExitCode, string Stdout, string Stderr, bool OutputLimited);

    private static async Task<ProcessResult> RunProcessAsync(string executable, string[] arguments, string input,
        string root, string home, CancellationToken token)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = root,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = new UTF8Encoding(false), StandardErrorEncoding = new UTF8Encoding(false)
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment.Clear();
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        foreach (var (key, value) in new Dictionary<string, string>
        {
            ["SystemRoot"] = windows, ["WINDIR"] = windows, ["ComSpec"] = Path.Combine(windows, "System32", "cmd.exe"),
            ["CODEX_HOME"] = home, ["USERPROFILE"] = Preparation.Under(home, "user"), ["HOME"] = Preparation.Under(home, "user"),
            ["APPDATA"] = Preparation.Under(home, "appdata"), ["LOCALAPPDATA"] = Preparation.Under(home, "localappdata"),
            ["TEMP"] = Preparation.Under(home, "temp"), ["TMP"] = Preparation.Under(home, "temp"),
            ["PATH"] = Path.GetDirectoryName(executable)! + ";" + Path.Combine(windows, "System32"), ["PATHEXT"] = ".EXE",
            ["DOTNET_GENERATE_ASPNET_CERTIFICATE"] = "false", ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_CLI_HOME"] = Preparation.Under(home, "dotnet-home"), ["NO_PROXY"] = "127.0.0.1,localhost"
        }) start.Environment[key] = value;
        using var process = new Process { StartInfo = start };
        process.Start();
        void Kill() { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } }
        using var stop = token.Register(Kill);
        var limited = 0;
        var count = 0;
        async Task<string> ReadAsync(StreamReader reader)
        {
            var saved = new StringBuilder();
            var buffer = new char[4096];
            while (true)
            {
                var length = await reader.ReadAsync(buffer);
                if (length == 0) return saved.ToString();
                var total = Interlocked.Add(ref count, length);
                var keep = Math.Clamp(1048576 - (total - length), 0, length);
                if (keep != 0) saved.Append(buffer, 0, keep);
                if (total <= 1048576) continue;
                Interlocked.Exchange(ref limited, 1); Kill();
            }
        }
        var stdout = ReadAsync(process.StandardOutput);
        var stderr = ReadAsync(process.StandardError);
        try
        {
            await process.StandardInput.WriteAsync(input.AsMemory(), token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(token);
            await Task.WhenAll(stdout, stderr);
            return new(process.ExitCode, await stdout, await stderr, Volatile.Read(ref limited) != 0);
        }
        finally
        {
            Kill();
            try { await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)); } catch (TimeoutException) { }
        }
    }

    private sealed class Stub
    {
        internal int ResponseRequests;
        internal bool ToolWasAdvertised;
        internal bool ShellWasAdvertised;
        internal bool CredentialHeaderPresent;
        internal string[] AdvertisedTools = [];
        internal string? CallNamespace;
        internal string? CallName;
        internal JsonElement? Execution;
        internal string? Failure;

        internal async Task ServeAsync(HttpListener listener, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var context = await listener.GetContextAsync().WaitAsync(token);
                    try { await ReplyAsync(context, token); }
                    finally { context.Response.Close(); }
                }
            }
            catch (Exception error) when (token.IsCancellationRequested && error is OperationCanceledException or HttpListenerException or ObjectDisposedException) { }
            catch (Exception error) { Failure = error.GetType().Name; }
        }

        private async Task ReplyAsync(HttpListenerContext context, CancellationToken token)
        {
            if (context.Request.RemoteEndPoint is not { } remote || !IPAddress.IsLoopback(remote.Address) ||
                context.Request.HttpMethod != "POST" || context.Request.Url?.AbsolutePath != "/v1/responses")
            { context.Response.StatusCode = 404; return; }
            CredentialHeaderPresent |= !string.IsNullOrEmpty(context.Request.Headers["Authorization"]);
            using var body = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                var length = await context.Request.InputStream.ReadAsync(buffer, token);
                if (length == 0) break;
                if (body.Length + length > 2097152) { context.Response.StatusCode = 413; return; }
                body.Write(buffer, 0, length);
            }
            using var request = JsonDocument.Parse(body.ToArray());
            ResponseRequests++;
            if (ResponseRequests > 2) { context.Response.StatusCode = 429; return; }
            var routes = request.RootElement.TryGetProperty("tools", out var tools) ? ReadToolRoutes(tools).ToArray() : [];
            AdvertisedTools = routes.Select(x => x.Namespace is null ? x.Name : x.Namespace + "." + x.Name).Distinct().ToArray();
            var selected = routes.FirstOrDefault(x => x.Namespace == ToolNamespace && x.Name == ToolFunction)
                ?? routes.FirstOrDefault(x => x.Namespace is null && x.Name == Tool);
            ToolWasAdvertised |= selected is not null;
            ShellWasAdvertised |= routes.Any(x => x.Name is "shell" or "local_shell" or "exec_command" or "write_stdin");
            context.Response.ContentType = "text/event-stream";
            context.Response.SendChunked = true;
            context.Response.Headers["Cache-Control"] = "no-cache";
            object item;
            if (ResponseRequests == 1)
            {
                if (selected is null)
                {
                    Failure = "expected-calculator-tool-not-advertised";
                    context.Response.StatusCode = 422;
                    return;
                }
                CallNamespace = selected.Namespace;
                CallName = selected.Name;
                var call = new Dictionary<string, object>
                {
                    ["id"] = "fc_tablecloth_local", ["type"] = "function_call", ["status"] = "completed",
                    ["call_id"] = Call, ["name"] = selected.Name, ["arguments"] = Input
                };
                if (selected.Namespace is not null) call["namespace"] = selected.Namespace;
                item = call;
            }
            else
            {
                if (request.RootElement.TryGetProperty("input", out var inputs)) Execution = FindExecution(inputs);
                item = new { id = "msg_tablecloth_local", type = "message", status = "completed", role = "assistant",
                    content = new[] { new { type = "output_text", text = Execution is not null ? Marker : "LOCAL_DETERMINISTIC_TOOL_ROUTING_FAILED", annotations = Array.Empty<object>() } } };
            }
            var response = new
            {
                id = "resp_tablecloth_local_" + ResponseRequests, @object = "response", created_at = 1792897200,
                status = "completed", model = "poc-local-deterministic", output = new[] { item },
                usage = new { input_tokens = 1, output_tokens = 1, total_tokens = 2, output_tokens_details = new { reasoning_tokens = 0 } }
            };
            await EventAsync(context.Response, "response.created", new { type = "response.created", response = new { response.id, @object = "response", status = "in_progress", output = Array.Empty<object>() } }, token);
            await EventAsync(context.Response, "response.output_item.done", new { type = "response.output_item.done", output_index = 0, item }, token);
            await EventAsync(context.Response, "response.completed", new { type = "response.completed", response }, token);
        }
    }

    private sealed record ToolRoute(string? Namespace, string Name);

    // Codex 0.156.1 protocol/models.rs separates FunctionCall.namespace and FunctionCall.name.
    // Namespace declarations group their leaf function specs in a nested tools array.
    private static IEnumerable<ToolRoute> ReadToolRoutes(JsonElement tools, string? parent = null)
    {
        if (tools.ValueKind != JsonValueKind.Array) yield break;
        foreach (var tool in tools.EnumerateArray())
        {
            if (tool.ValueKind != JsonValueKind.Object) continue;
            var type = tool.TryGetProperty("type", out var kind) && kind.ValueKind == JsonValueKind.String ? kind.GetString() : null;
            var name = tool.TryGetProperty("name", out var named) && named.ValueKind == JsonValueKind.String ? named.GetString() : null;
            if (type == "namespace" && parent is null && !string.IsNullOrEmpty(name) && tool.TryGetProperty("tools", out var nested))
            {
                foreach (var route in ReadToolRoutes(nested, name)) yield return route;
            }
            else
            {
                if (name is null && tool.TryGetProperty("function", out var function) && function.ValueKind == JsonValueKind.Object &&
                    function.TryGetProperty("name", out named) && named.ValueKind == JsonValueKind.String) name = named.GetString();
                if (!string.IsNullOrEmpty(name ?? type)) yield return new(parent, name ?? type!);
            }
        }
    }

    private static async Task EventAsync(HttpListenerResponse response, string name, object value, CancellationToken token)
    {
        var bytes = Encoding.UTF8.GetBytes("event: " + name + "\ndata: " + JsonSerializer.Serialize(value) + "\n\n");
        await response.OutputStream.WriteAsync(bytes, token);
        await response.OutputStream.FlushAsync(token);
    }

    private static JsonElement? FindExecution(JsonElement value, int depth = 0)
    {
        if (depth > 20) return null;
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (value.TryGetProperty("wasAppContainer", out var container) && container.ValueKind == JsonValueKind.True &&
                value.TryGetProperty("status", out var status) && status.GetString() == "completed" &&
                value.TryGetProperty("exitCode", out var exit) && exit.TryGetInt32(out var code) && code == 0 &&
                value.TryGetProperty("stdout", out var output) && output.ValueKind == JsonValueKind.String)
            {
                try
                {
                    using var result = JsonDocument.Parse(output.GetString()!);
                    if (result.RootElement.GetProperty("ok").GetBoolean() && result.RootElement.GetProperty("result").GetProperty("final").GetString() == "80.00")
                        return value.Clone();
                }
                catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException) { }
            }
            foreach (var property in value.EnumerateObject())
                if (FindExecution(property.Value, depth + 1) is { } found) return found;
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in value.EnumerateArray())
                if (FindExecution(child, depth + 1) is { } found) return found;
        }
        else if (value.ValueKind == JsonValueKind.String && value.GetString() is { Length: < 1048576 } text &&
                 text.TrimStart().StartsWith('{'))
        {
            try { using var parsed = JsonDocument.Parse(text); return FindExecution(parsed.RootElement, depth + 1); }
            catch (JsonException) { }
        }
        return null;
    }

    private static bool HasCompletedMcpEvent(string lines) => Events(lines).Any(value =>
        value.TryGetProperty("type", out var type) && type.GetString() == "item.completed" &&
        value.TryGetProperty("item", out var item) && item.TryGetProperty("type", out var kind) && kind.GetString() == "mcp_tool_call" &&
        item.TryGetProperty("server", out var server) && server.GetString() == "tablecloth_skill_poc" &&
        item.TryGetProperty("tool", out var tool) && tool.GetString() is "tablecloth-calculators" or ToolFunction && FindExecution(item) is not null);

    private static string? McpEventToolName(string lines) => Events(lines)
        .Where(value => value.TryGetProperty("type", out var type) && type.GetString() == "item.completed" &&
            value.TryGetProperty("item", out var item) && item.TryGetProperty("type", out var kind) && kind.GetString() == "mcp_tool_call" &&
            item.TryGetProperty("server", out var server) && server.GetString() == "tablecloth_skill_poc")
        .Select(value => value.GetProperty("item").TryGetProperty("tool", out var tool) ? tool.GetString() : null).FirstOrDefault();

    private static bool HasFinalEvent(string lines) => Events(lines).Any(value =>
        value.TryGetProperty("type", out var type) && type.GetString() == "item.completed" &&
        value.TryGetProperty("item", out var item) && item.TryGetProperty("type", out var kind) && kind.GetString() == "agent_message" &&
        item.TryGetProperty("text", out var text) && text.GetString() == Marker);

    private static IEnumerable<JsonElement> Events(string lines)
    {
        foreach (var line in lines.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            JsonElement? value = null;
            try { using var parsed = JsonDocument.Parse(line); value = parsed.RootElement.Clone(); } catch (JsonException) { }
            if (value is { } item) yield return item;
        }
    }

    private static string Toml(string value) => JsonSerializer.Serialize(value);

    private static int FreeLoopbackPort()
    {
        using var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start(); return ((IPEndPoint)tcp.LocalEndpoint).Port;
    }

    private static string FindWorkspace()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "tools", "appcontainer-skill-poc", "Poc.csproj"))) return directory.FullName;
        throw new InvalidDataException("The project workspace could not be located.");
    }
}
