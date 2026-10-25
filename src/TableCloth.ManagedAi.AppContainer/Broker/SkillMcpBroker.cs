using System.Text;
using System.Text.Json;

namespace TableCloth.AppContainerPoc;

/// <summary>Bounded stdio MCP transport. Isolation comes from the native runner, not MCP.</summary>
public static class SkillMcpBroker
{
    private const int MaximumRequestBytes = 131072;
    private const int MaximumResponseBytes = 1048576;
    private const string ProtocolVersion = "2025-06-18";

    public static async Task<int> RunAsync(string manifestPath, CancellationToken token)
    {
        try
        {
            var registry = await SkillExecutionRegistry.LoadAsync(manifestPath, token);
            return await RunAsync(registry, Console.OpenStandardInput(), Console.OpenStandardOutput(), token);
        }
        catch (OperationCanceledException) { return 130; }
        catch
        {
            await Console.Error.WriteLineAsync("broker: registration or transport failed.");
            return 2;
        }
    }

    // Stream injection allows offline transport tests without altering the process console or starting workers.
    public static async Task<int> RunAsync(SkillExecutionRegistry registry, Stream input, Stream output, CancellationToken token)
    {
        var reader = new BoundedLines(input, MaximumRequestBytes);
        var initialized = false;
        var ready = false;
        for (var count = 0; count < 1024; count++)
        {
            var line = await reader.ReadAsync(token);
            if (line.End) return 0;
            if (line.Invalid)
            {
                await SendErrorAsync(output, null, -32700, "Invalid or oversized JSON message.", token);
                return 2;
            }
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line.Text!, new JsonDocumentOptions { MaxDepth = 32 });
                BrokerFiles.ValidateJson(document.RootElement);
            }
            catch (Exception error) when (error is JsonException or BrokerValidationException)
            {
                await SendErrorAsync(output, null, -32700, "Invalid JSON message.", token);
                continue;
            }
            using (document)
            {
                var root = document.RootElement;
                JsonElement? id = null;
                string method;
                JsonElement parameters = default;
                try
                {
                    BrokerFiles.ObjectFields(root, ["jsonrpc", "id", "method", "params"]);
                    if (BrokerFiles.Text(root, "jsonrpc", 3) != "2.0") throw BrokerFiles.Invalid();
                    method = BrokerFiles.Text(root, "method", 80);
                    if (root.TryGetProperty("id", out var requestId))
                    {
                        if (requestId.ValueKind == JsonValueKind.String && requestId.GetString()!.Length <= 128 ||
                            requestId.ValueKind == JsonValueKind.Number && requestId.TryGetInt64(out _)) id = requestId;
                        else throw BrokerFiles.Invalid();
                    }
                    if (root.TryGetProperty("params", out parameters) && parameters.ValueKind != JsonValueKind.Object) throw BrokerFiles.Invalid();
                }
                catch (BrokerValidationException)
                {
                    await SendErrorAsync(output, null, -32600, "Invalid JSON-RPC request.", token);
                    continue;
                }
                if (id is null)
                {
                    if (method == "notifications/initialized" && initialized) ready = true;
                    // JSON-RPC notifications never receive a response; no notification can execute a tool.
                    continue;
                }
                try
                {
                    if (method == "initialize")
                    {
                        if (initialized) throw new RpcFailure(-32600, "Already initialized.");
                        if (parameters.ValueKind != JsonValueKind.Object) throw new RpcFailure(-32602, "Invalid initialization parameters.");
                        var requested = BrokerFiles.Text(parameters, "protocolVersion", 32);
                        if (!parameters.TryGetProperty("capabilities", out var capabilities) || capabilities.ValueKind != JsonValueKind.Object ||
                            !parameters.TryGetProperty("clientInfo", out var clientInfo) || clientInfo.ValueKind != JsonValueKind.Object)
                            throw new RpcFailure(-32602, "Invalid initialization parameters.");
                        _ = BrokerFiles.Text(clientInfo, "name", 128);
                        _ = BrokerFiles.Text(clientInfo, "version", 64);
                        var version = requested is "2024-11-05" or "2025-03-26" or ProtocolVersion ? requested : ProtocolVersion;
                        await SendResultAsync(output, id.Value, writer =>
                        {
                            writer.WriteStartObject();
                            writer.WriteString("protocolVersion", version);
                            writer.WritePropertyName("capabilities"); writer.WriteStartObject();
                            writer.WritePropertyName("tools"); writer.WriteStartObject(); writer.WriteBoolean("listChanged", false); writer.WriteEndObject();
                            writer.WriteEndObject();
                            writer.WritePropertyName("serverInfo"); writer.WriteStartObject();
                            writer.WriteString("name", "tablecloth-appcontainer-skills"); writer.WriteString("version", "0.1.0"); writer.WriteEndObject();
                            writer.WriteString("instructions", "Experimental fixed registered scripts run through the native AppContainer runner. Network-required registrations are blocked. MCP itself does not provide isolation.");
                            writer.WriteEndObject();
                        }, token);
                        initialized = true;
                    }
                    else if (method == "ping")
                        await SendResultAsync(output, id.Value, writer => { writer.WriteStartObject(); writer.WriteEndObject(); }, token);
                    else
                    {
                        if (!ready) throw new RpcFailure(-32002, "Initialization is not complete.");
                        if (method == "tools/list")
                        {
                            if (parameters.ValueKind == JsonValueKind.Object && parameters.TryGetProperty("cursor", out _))
                                throw new RpcFailure(-32602, "Pagination cursors are not supported.");
                            await SendResultAsync(output, id.Value, writer => WriteTools(writer, registry.Skills), token);
                        }
                        else if (method == "tools/call")
                        {
                            if (parameters.ValueKind != JsonValueKind.Object) throw new RpcFailure(-32602, "Invalid tool parameters.");
                            BrokerFiles.ObjectFields(parameters, ["name", "arguments", "_meta"]);
                            var name = BrokerFiles.Text(parameters, "name", 64);
                            if (!registry.Skills.Any(x => x.SkillId == name)) throw new RpcFailure(-32602, "Unknown registered tool.");
                            using var empty = JsonDocument.Parse("{}");
                            var arguments = parameters.TryGetProperty("arguments", out var supplied) ? supplied : empty.RootElement;
                            if (arguments.ValueKind != JsonValueKind.Object) throw new RpcFailure(-32602, "Tool arguments must be an object.");
                            var execution = await registry.ExecuteAsync(name, arguments, token);
                            await SendResultAsync(output, id.Value, writer => WriteToolResult(writer, execution), token);
                        }
                        else throw new RpcFailure(-32601, "Method not found.");
                    }
                }
                catch (RpcFailure error) { await SendErrorAsync(output, id, error.Code, error.Message, token); }
                catch (BrokerValidationException) { await SendErrorAsync(output, id, -32602, "Invalid method parameters.", token); }
                catch (OperationCanceledException) { throw; }
                catch { await SendErrorAsync(output, id, -32603, "Internal broker failure.", token); }
            }
        }
        await Console.Error.WriteLineAsync("broker: session request limit reached.");
        return 2;
    }

    private static void WriteTools(Utf8JsonWriter writer, IReadOnlyList<SkillRegistration> skills)
    {
        writer.WriteStartObject(); writer.WritePropertyName("tools"); writer.WriteStartArray();
        foreach (var skill in skills)
        {
            writer.WriteStartObject();
            writer.WriteString("name", skill.SkillId);
            var description = skill.Kind == "python-json-stdin" ? "Run the registered deterministic calculator with one JSON operation. Six examples: percent.part {total,rate_percent}; discount.single {price,rate_percent}; loan {principal,annual_rate_percent,periods,periods_per_year,method}; date.add {date,years?,months?,days?}; time.convert {datetime,from_offset,to_offset}; bmi {weight_kg,height_cm}. Dates use YYYY-MM-DD; datetimes use YYYY-MM-DDTHH:MM; offsets use +/-HH:MM. All rates are user-supplied percentages, with taxes and fees excluded. BMI returns a number without medical classification."
                : skill.Kind == "python-cli-fixed" ? "Run a reviewed script with manifest-fixed arguments; input must be an empty object."
                : "Run the registered Node.js JSON-stdin evaluation fixture.";
            if (skill.RequiresNetwork) description += " BLOCKED: this registration requires network access, which this broker never grants.";
            writer.WriteString("description", description);
            writer.WritePropertyName("inputSchema"); writer.WriteStartObject(); writer.WriteString("type", "object");
            writer.WritePropertyName("properties"); writer.WriteStartObject();
            if (skill.AllowedOperations.Count != 0)
            {
                writer.WritePropertyName("operation"); writer.WriteStartObject(); writer.WriteString("type", "string");
                writer.WritePropertyName("enum"); writer.WriteStartArray(); foreach (var operation in skill.AllowedOperations) writer.WriteStringValue(operation); writer.WriteEndArray(); writer.WriteEndObject();
            }
            if (skill.Kind == "python-json-stdin") WriteCalculatorProperties(writer);
            if (skill.Kind == "node-json-stdin")
                foreach (var field in new[] { "a", "b" })
                { writer.WritePropertyName(field); writer.WriteStartObject(); writer.WriteString("type", "number"); writer.WriteEndObject(); }
            writer.WriteEndObject();
            if (skill.AllowedOperations.Count != 0)
            { writer.WritePropertyName("required"); writer.WriteStartArray(); writer.WriteStringValue("operation"); writer.WriteEndArray(); }
            if (skill.Kind == "node-json-stdin")
            { writer.WritePropertyName("required"); writer.WriteStartArray(); writer.WriteStringValue("a"); writer.WriteStringValue("b"); writer.WriteEndArray(); }
            writer.WriteBoolean("additionalProperties", skill.Kind == "python-json-stdin");
            if (skill.Kind == "python-json-stdin") WriteCalculatorRequirements(writer, skill.AllowedOperations);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        writer.WriteEndArray(); writer.WriteEndObject();
    }

    private static void WriteCalculatorProperties(Utf8JsonWriter writer)
    {
        foreach (var (name, description) in new[]
        {
            ("total", "Required by percent.part; the base value."),
            ("rate_percent", "Required by percent.part and discount.single; 20 means 20 percent. Discount rates range from 0 to 100."),
            ("price", "Required by discount.single; the original nonnegative price."),
            ("principal", "Required by interest.simple and loan; initial amount, positive for a loan."),
            ("annual_rate_percent", "Required by interest.simple and loan; user-supplied nominal annual percentage, from 0 to 1000."),
            ("years", "Required by interest.simple; nonnegative duration. Optional integer for date.add."),
            ("periods", "Required by loan; integer payment count from 1 to 1200."),
            ("periods_per_year", "Required by loan; integer payments per year from 1 to 366, for example 12 for monthly payments."),
            ("months", "Optional signed integer month shift for date.add; the original day anchors month-end clamping."),
            ("days", "Optional signed integer day shift for date.add, applied after years and months."),
            ("places", "Optional currency decimal places, an integer from 0 to 6; default 2 with half-up rounding."),
            ("weight_kg", "Required by bmi; positive weight in kilograms."),
            ("height_cm", "Required by bmi; positive height in centimetres. Result is a BMI number only, not a diagnosis.")
        })
        {
            writer.WritePropertyName(name); writer.WriteStartObject();
            writer.WritePropertyName("type"); writer.WriteStartArray(); writer.WriteStringValue("number"); writer.WriteStringValue("string"); writer.WriteEndArray();
            writer.WriteString("description", description); writer.WriteEndObject();
        }
        foreach (var (name, description) in new[]
        {
            ("method", "Required by loan: annuity, equal_principal, or bullet."),
            ("schedule", "Optional for loan: summary (default) or full. Large full schedules can exceed this broker's output limit."),
            ("date", "Required by date.add; Gregorian YYYY-MM-DD."),
            ("datetime", "Required by time.convert; timezone-free YYYY-MM-DDTHH:MM with optional seconds and microseconds."),
            ("from_offset", "Required by time.convert; fixed UTC offset such as +09:00 or -05:30. Named zones and DST are unsupported."),
            ("to_offset", "Required by time.convert; fixed UTC offset such as +00:00 or +05:45.")
        })
        {
            writer.WritePropertyName(name); writer.WriteStartObject(); writer.WriteString("type", "string");
            writer.WriteString("description", description); writer.WriteEndObject();
        }
    }

    private static void WriteCalculatorRequirements(Utf8JsonWriter writer, IReadOnlyList<string> allowed)
    {
        writer.WritePropertyName("allOf"); writer.WriteStartArray();
        foreach (var (operation, required) in new[]
        {
            ("percent.part", new[] { "total", "rate_percent" }),
            ("discount.single", new[] { "price", "rate_percent" }),
            ("loan", new[] { "principal", "annual_rate_percent", "periods", "periods_per_year", "method" }),
            ("date.add", new[] { "date" }),
            ("time.convert", new[] { "datetime", "from_offset", "to_offset" }),
            ("bmi", new[] { "weight_kg", "height_cm" })
        })
        {
            if (!allowed.Contains(operation, StringComparer.Ordinal)) continue;
            writer.WriteStartObject(); writer.WritePropertyName("if"); writer.WriteStartObject();
            writer.WritePropertyName("properties"); writer.WriteStartObject(); writer.WritePropertyName("operation");
            writer.WriteStartObject(); writer.WriteString("const", operation); writer.WriteEndObject(); writer.WriteEndObject(); writer.WriteEndObject();
            writer.WritePropertyName("then"); writer.WriteStartObject(); writer.WritePropertyName("required"); writer.WriteStartArray();
            foreach (var field in required) writer.WriteStringValue(field);
            writer.WriteEndArray(); writer.WriteEndObject(); writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private static void WriteToolResult(Utf8JsonWriter writer, SkillExecutionResult result)
    {
        // One bounded text block works with older MCP clients and preserves the execution evidence.
        var text = Serialize(writer => WriteExecution(writer, result));
        writer.WriteStartObject(); writer.WriteBoolean("isError", result.IsError);
        writer.WritePropertyName("content"); writer.WriteStartArray(); writer.WriteStartObject();
        writer.WriteString("type", "text"); writer.WriteString("text", Encoding.UTF8.GetString(text));
        writer.WriteEndObject(); writer.WriteEndArray(); writer.WriteEndObject();
    }

    public static void WriteExecution(Utf8JsonWriter writer, SkillExecutionResult result)
    {
        writer.WriteStartObject(); writer.WriteString("status", result.Status);
        if (result.ExitCode is { } exitCode) writer.WriteNumber("exitCode", exitCode); else writer.WriteNull("exitCode");
        writer.WriteString("stdout", result.Stdout); writer.WriteString("stderr", result.Stderr);
        writer.WriteBoolean("wasAppContainer", result.WasAppContainer); writer.WriteBoolean("timedOut", result.TimedOut);
        writer.WriteBoolean("outputLimited", result.OutputLimited);
        if (result.Failure is null) writer.WriteNull("failure"); else writer.WriteString("failure", result.Failure);
        writer.WriteEndObject();
    }

    private static Task SendResultAsync(Stream output, JsonElement id, Action<Utf8JsonWriter> result, CancellationToken token)
        => SendAsync(output, writer =>
        {
            writer.WriteStartObject(); writer.WriteString("jsonrpc", "2.0"); writer.WritePropertyName("id"); id.WriteTo(writer);
            writer.WritePropertyName("result"); result(writer); writer.WriteEndObject();
        }, token);

    private static Task SendErrorAsync(Stream output, JsonElement? id, int code, string message, CancellationToken token)
        => SendAsync(output, writer =>
        {
            writer.WriteStartObject(); writer.WriteString("jsonrpc", "2.0"); writer.WritePropertyName("id");
            if (id is { } value) value.WriteTo(writer); else writer.WriteNullValue();
            writer.WritePropertyName("error"); writer.WriteStartObject(); writer.WriteNumber("code", code); writer.WriteString("message", message);
            writer.WriteEndObject(); writer.WriteEndObject();
        }, token);

    private static async Task SendAsync(Stream output, Action<Utf8JsonWriter> write, CancellationToken token)
    {
        var bytes = Serialize(write);
        if (bytes.Length > MaximumResponseBytes) throw new BrokerValidationException("Response limit exceeded.");
        await output.WriteAsync(bytes, token);
        await output.WriteAsync(new byte[] { (byte)'\n' }, token);
        await output.FlushAsync(token);
    }

    private static byte[] Serialize(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) write(writer);
        return stream.ToArray();
    }

    private sealed class RpcFailure(int code, string message) : Exception(message) { internal int Code => code; }

    private sealed class BoundedLines(Stream stream, int maximum)
    {
        private readonly byte[] _buffer = new byte[8192];
        private readonly UTF8Encoding _encoding = new(false, true);
        private int _position;
        private int _available;
        internal async Task<(string? Text, bool Invalid, bool End)> ReadAsync(CancellationToken token)
        {
            using var line = new MemoryStream();
            while (true)
            {
                if (_position == _available)
                {
                    _available = await stream.ReadAsync(_buffer, token);
                    _position = 0;
                    if (_available == 0)
                    {
                        if (line.Length == 0) return (null, false, true);
                        break;
                    }
                }
                var value = _buffer[_position++];
                if (value == (byte)'\n') break;
                if (line.Length >= maximum) return (null, true, false);
                line.WriteByte(value);
            }
            try { return (_encoding.GetString(line.GetBuffer(), 0, (int)line.Length).TrimEnd('\r'), false, false); }
            catch (DecoderFallbackException) { return (null, true, false); }
        }
    }
}
