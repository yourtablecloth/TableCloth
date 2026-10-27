using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TableCloth.SkillSmokeDiagnostics;

public sealed record EvidenceFileDigest(string File, long Bytes, string Sha256);

public sealed record VerificationReport(bool Passed, IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures, IReadOnlyList<EvidenceFileDigest> EvidenceFiles)
{
    public string? ExpectedProductSha256 { get; init; }
    public bool ProductHashExternallyAnchored => ExpectedProductSha256 is not null;
    public string RuntimeStorageLayout { get; init; } = "unrecognized";
    public bool HistoricalStorageLayout => RuntimeStorageLayout == "legacy-installation-root";
    public string RuntimeStorageBoundary => HistoricalStorageLayout
        ? "Historical offline evidence from the former installation-owned data location; this does not qualify the current update-safe storage configuration."
        : "Current live checks require TableCloth.Data/ManagedAi outside the installer-owned TableCloth directory.";
    public string VerificationBoundary => "Offline consistency verification cannot cryptographically attest files from a malicious producer. Run the independent Win32 process/token observer during live execution; JSON wasAppContainer or model self-report alone is insufficient.";
}

/// <summary>Read-only reconstruction from raw protocol and independent process observations.</summary>
public static class EvidenceVerifier
{
    private const int MaximumFileBytes = 4 * 1024 * 1024;
    private const long MaximumBundleBytes = 64L * 1024 * 1024;
    private static readonly string[] SkillIds = ["bok-ecos-stats", "kosis-stats", "official-report-skillset",
        "tablecloth-certificate-expiry", "tablecloth-windows-sandbox", "tablecloth-calculators"];
    private static readonly string[] ScriptIds = ["bok-ecos-stats", "kosis-stats", "tablecloth-calculators"];
    private static readonly JsonDocumentOptions JsonOptions = new() { MaxDepth = 32 };

    public static VerificationReport Verify(string evidenceDirectory, string? expectedProductSha256 = null)
    {
        var checks = new List<string>();
        var failures = new List<string>();
        var digests = new List<EvidenceFileDigest>();
        var storageLayout = "unrecognized";
        void Check(string name, bool passed) { checks.Add(name); if (!passed) failures.Add(name); }
        try
        {
            var files = ReadBundle(evidenceDirectory, digests);
            var log = Lines(Required(files, "probe.jsonl"));
            var product = OneStage(log, "product-host").GetProperty("details");
            var productPath = LocalPath(Text(product, "executable"));
            var productHash = Text(product, "sha256");
            Check("product-host", ValidHash(productHash) && Path.GetFileName(productPath).Equals("TableCloth.exe", StringComparison.OrdinalIgnoreCase));
            Check("product-hash-anchor", expectedProductSha256 is null || ValidHash(expectedProductSha256) && productHash.Equals(expectedProductSha256, StringComparison.OrdinalIgnoreCase));
            var seed = Parse(Required(files, "runtime-seed-manifest.json"));
            var owned = LocalPath(Text(seed, "ownedRoot"));
            // The read-only verifier can review older bundles, but reports their unsafe historical
            // storage layout explicitly. Live fixtures accept only the new update-safe data root.
            if (HasStorageSuffix(owned, "TableCloth.Data")) storageLayout = "update-safe-data-root";
            else if (HasStorageSuffix(owned, "TableCloth")) storageLayout = "legacy-installation-root";
            Check("owned-runtime-root", storageLayout != "unrecognized");
            var python = LocalPath(Text(seed, "pythonExecutable"));
            var pythonHash = Text(seed, "pythonSha256");
            var node = LocalPath(Text(seed, "nodeExecutable"));
            var nodeHash = Text(seed, "nodeSha256");
            Check("registered-runtime-paths", Under(Path.Combine(owned, "runtimes"), python) && Path.GetFileName(python).Equals("python.exe", StringComparison.OrdinalIgnoreCase) &&
                Under(Path.Combine(owned, "runtimes"), node) && Path.GetFileName(node).Equals("node.exe", StringComparison.OrdinalIgnoreCase) && ValidHash(pythonHash) && ValidHash(nodeHash));
            var runtimeFiles = seed.GetProperty("runtimeFiles");
            Check("registered-runtime-executable-hashes", runtimeFiles.ValueKind == JsonValueKind.Array &&
                new[] { (python, pythonHash), (node, nodeHash) }.All(expected => runtimeFiles.EnumerateArray().Count(file =>
                    LocalPath(Text(file, "path")).Equals(expected.Item1, StringComparison.OrdinalIgnoreCase) && Text(file, "sha256").Equals(expected.Item2, StringComparison.OrdinalIgnoreCase)) == 1));
            var appRoot = Path.GetDirectoryName(owned)!;
            var codex = OneStage(log, "codex-runtime").GetProperty("details");
            Check("app-owned-codex-runtime", True(codex, "installed") && Under(Path.Combine(appRoot, "runtimes", "openai-codex"), LocalPath(Text(codex, "executable"))) &&
                LocalPath(Text(codex, "profile")).Equals(Path.Combine(appRoot, "profiles", "openai-codex", "codex-home"), StringComparison.OrdinalIgnoreCase));
            var policy = OneStage(log, "profile-policy").GetProperty("details");
            Check("disabled-shell-and-unified-policy", False(policy, "shellToolEnabled") && False(policy, "unifiedExecEnabled") && True(policy, "actualConfigContainsDisabledFeatures"));
            Check("probe-lifecycle-complete", log.Count(x => OptionalText(x, "stage") == "completed") == 1 && !log.Any(x => OptionalText(x, "stage") is "failure" or "chat-failure"));
            foreach (var id in SkillIds)
            {
                try
                {
                    var requestLog = OneCaseStage(log, "chat-request", id);
                    var resultLog = OneCaseStage(log, "chat-result", id);
                    var start = Timestamp(requestLog, "timestampUtc");
                    var end = Timestamp(resultLog, "timestampUtc");
                    var requestDetails = requestLog.GetProperty("details");
                    Check(id + ":lifecycle", end >= start && False(requestDetails, "realPrivateCertificateScan") && False(requestDetails, "realWindowsSandboxOperation"));
                    var stream = Lines(Required(files, id + ".provider.events.jsonl"));
                    var evidence = InspectStream(id, stream);
                    Check(id + ":raw-protocol", evidence.Valid);
                    var observations = Parse(Required(files, id + ".processes.json"));
                    if (observations.ValueKind == JsonValueKind.Object) observations = observations.GetProperty("Observations");
                    Check(id + ":process-observations-array", observations.ValueKind == JsonValueKind.Array);
                    if (ScriptIds.Contains(id, StringComparer.Ordinal))
                        Check(id + ":independent-worker-token", ObserveWorker(observations, productPath, productHash, python, pythonHash, start, end, evidence.Calls));
                    else
                    {
                        Check(id + ":response-only-evidence", evidence.Calls == 0 &&
                            (id != "tablecloth-certificate-expiry" || True(requestDetails, "certificateReportIsSynthetic")) &&
                            (id != "tablecloth-windows-sandbox" || True(requestDetails, "sandboxReportIsSynthetic")));
                    }
                }
                catch (Exception error) when (Expected(error)) { Check(id + ":complete-raw-evidence", false); }
            }
        }
        catch (Exception error) when (Expected(error)) { Check("bundle-readable-and-valid", false); }
        return new(failures.Count == 0, checks.AsReadOnly(), failures.AsReadOnly(), digests.AsReadOnly())
            { ExpectedProductSha256 = expectedProductSha256, RuntimeStorageLayout = storageLayout };
    }

    public static bool IsValidKosisSearchData(string stdout)
    {
        try
        {
            var root = Parse(stdout);
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 1 || root[0].ValueKind != JsonValueKind.Object) return false;
            var row = root[0];
            if (row.TryGetProperty("error", out _) || row.TryGetProperty("err", out _) || row.TryGetProperty("RESULT", out _)) return false;
            var organization = row.GetProperty("ORG_ID");
            var validOrganization = organization.ValueKind == JsonValueKind.String
                ? Regex.IsMatch(organization.GetString()!, "\\A[0-9]{1,16}\\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)) &&
                    ulong.TryParse(organization.GetString(), out var number) && number > 0
                : organization.ValueKind == JsonValueKind.Number && organization.TryGetUInt64(out var numeric) && numeric > 0;
            return validOrganization && row.GetProperty("TBL_ID").ValueKind == JsonValueKind.String &&
                Regex.IsMatch(row.GetProperty("TBL_ID").GetString()!, "\\A[A-Za-z0-9_-]{1,128}\\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }
        catch (Exception error) when (Expected(error)) { return false; }
    }

    private static (bool Valid, int Calls) InspectStream(string id, IReadOnlyList<JsonElement> events)
    {
        var starts = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var completedIds = new HashSet<string>(StringComparer.Ordinal);
        var answer = new StringBuilder();
        var valid = events.Count(x => OptionalText(x, "type") == "turn.started") == 1 && events.Count(x => OptionalText(x, "type") == "turn.completed") == 1;
        var calls = 0;
        var script = ScriptIds.Contains(id, StringComparer.Ordinal);
        foreach (var entry in events)
        {
            var type = OptionalText(entry, "type");
            if (type is "turn.failed" or "error") valid = false;
            if (type is not ("item.started" or "item.completed")) continue;
            var item = entry.GetProperty("item");
            var itemType = Text(item, "type");
            if (itemType == "command_execution") { valid = false; continue; }
            if (itemType == "agent_message" && type == "item.completed") { answer.AppendLine(Text(item, "text")); continue; }
            if (itemType != "mcp_tool_call") continue;
            if (!script || Text(item, "server") != "tablecloth_skills" || Text(item, "tool") != id || !ValidRequest(id, item.GetProperty("arguments"))) { valid = false; continue; }
            var itemId = Text(item, "id");
            if (itemId.Length is < 1 or > 128) { valid = false; continue; }
            if (type == "item.started")
            {
                if (Text(item, "status") != "in_progress" || !Null(item, "error") || !Null(item, "result") || !starts.TryAdd(itemId, item)) valid = false;
                continue;
            }
            if (!starts.Remove(itemId, out var started) || !completedIds.Add(itemId) ||
                !JsonElement.DeepEquals(started.GetProperty("arguments"), item.GetProperty("arguments")) ||
                Text(item, "status") != "completed" || !Null(item, "error")) { valid = false; continue; }
            var result = item.GetProperty("result");
            if (result.ValueKind != JsonValueKind.Object || result.TryGetProperty("isError", out var isError) && isError.ValueKind != JsonValueKind.False) { valid = false; continue; }
            var blocks = result.GetProperty("content");
            if (blocks.ValueKind != JsonValueKind.Array || blocks.GetArrayLength() != 1 || Text(blocks[0], "type") != "text") { valid = false; continue; }
            var execution = Parse(Text(blocks[0], "text"));
            if (Text(execution, "status") != "completed" || !execution.GetProperty("exitCode").TryGetInt32(out var exitCode) || exitCode != 0 ||
                !True(execution, "wasAppContainer") || !False(execution, "timedOut") || !False(execution, "outputLimited") || !Null(execution, "failure") ||
                execution.GetProperty("stdout").ValueKind != JsonValueKind.String || execution.GetProperty("stderr").ValueKind != JsonValueKind.String ||
                !ValidScriptOutput(id, Text(execution, "stdout"))) { valid = false; continue; }
            calls++;
        }
        valid &= starts.Count == 0;
        if (script) return (valid && calls > 0, calls);
        var response = answer.ToString();
        var interpretation = id switch
        {
            "official-report-skillset" => new[] { "추진 배경", "추진 경과", "주요 내용", "향후 추진 계획", "붙임" }.All(response.Contains),
            "tablecloth-certificate-expiry" => response.Contains('1') && response.Contains('2') && response.Contains("30", StringComparison.Ordinal) && response.Contains("합성", StringComparison.Ordinal),
            "tablecloth-windows-sandbox" => (response.Contains("0개", StringComparison.Ordinal) || response.Contains("없", StringComparison.Ordinal)) && response.Contains("합성", StringComparison.Ordinal),
            _ => false
        };
        return (valid && interpretation && calls == 0, calls);
    }

    private static bool ValidRequest(string id, JsonElement request)
    {
        if (request.ValueKind != JsonValueKind.Object || request.EnumerateObject().Any(x => x.Name is not ("arguments" or "stdin"))) return false;
        var args = Array.Empty<string>();
        if (request.TryGetProperty("arguments", out var supplied))
        {
            if (supplied.ValueKind != JsonValueKind.Array || supplied.GetArrayLength() > 64 || supplied.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String)) return false;
            args = supplied.EnumerateArray().Select(x => x.GetString()!).ToArray();
        }
        if (id == "tablecloth-calculators")
        {
            if (args.Length != 0) return false;
            var input = Parse(Text(request, "stdin"));
            return input.ValueKind == JsonValueKind.Object && input.EnumerateObject().All(x => x.Name is "operation" or "height_cm" or "weight_kg") &&
                Text(input, "operation") == "bmi" && Decimal(input.GetProperty("height_cm")) == 171 && Decimal(input.GetProperty("weight_kg")) == 80;
        }
        if (request.TryGetProperty("stdin", out var stdin) && (stdin.ValueKind != JsonValueKind.String || !string.IsNullOrWhiteSpace(stdin.GetString()))) return false;
        if (args.Length == 0 || args[0] != (id == "bok-ecos-stats" ? "key" : "search")) return false;
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index++)
        {
            var flag = args[index];
            if (flag is "--text" or "--json") { if (!options.TryAdd(flag, "")) return false; continue; }
            if (flag is not ("--limit" or "--timeout" or "--secrets-path" or "--query" or "--result-count") || ++index >= args.Length || !options.TryAdd(flag, args[index])) return false;
        }
        if (options.TryGetValue("--timeout", out var timeout) && (!int.TryParse(timeout, out var seconds) || seconds is < 1 or > 30)) return false;
        return id == "bok-ecos-stats"
            ? options.TryGetValue("--limit", out var limit) && limit == "1" && !options.ContainsKey("--query") && !options.ContainsKey("--result-count")
            : options.TryGetValue("--query", out var query) && query == "인구" && options.TryGetValue("--result-count", out var count) && count == "1" && !options.ContainsKey("--limit") && !options.ContainsKey("--secrets-path");
    }

    private static bool ValidScriptOutput(string id, string stdout)
    {
        if (id == "kosis-stats") return IsValidKosisSearchData(stdout);
        if (id == "bok-ecos-stats")
        {
            var line = stdout.TrimEnd('\r', '\n');
            if (line.Length <= 4096 && !line.Any(char.IsControl))
            {
                var match = Regex.Match(line, @"\A\[(?<category>[^\]\r\n]+)\] (?<name>[^\r\n]+): (?<value>[+-]?(?:[0-9]{1,3}(?:,[0-9]{3})+|[0-9]+)(?:\.[0-9]+)?) (?<unit>[^\r\n]+) \((?<cycle>[0-9]{8})\)\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(100));
                if (match.Success && new[] { "category", "name", "unit" }.All(name => !string.IsNullOrWhiteSpace(match.Groups[name].Value)) &&
                    decimal.TryParse(match.Groups["value"].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out _) &&
                    DateOnly.TryParseExact(match.Groups["cycle"].Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return true;
            }
        }
        var root = Parse(stdout);
        if (id == "bok-ecos-stats")
        {
            if (Text(root, "result") != "ok" || root.GetProperty("rows").ValueKind != JsonValueKind.Array || root.GetProperty("rows").GetArrayLength() != 1) return false;
            var row = root.GetProperty("rows")[0];
            return !string.IsNullOrWhiteSpace(Text(row, "class_name")) && !string.IsNullOrWhiteSpace(Text(row, "name")) && !string.IsNullOrWhiteSpace(Text(row, "unit")) &&
                Decimal(row.GetProperty("value")) is not null && DateOnly.TryParseExact(Text(row, "cycle"), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        }
        return True(root, "ok") && Text(root, "operation") == "bmi" && Decimal(root.GetProperty("result").GetProperty("bmi")) == 27.36m;
    }

    private static bool ObserveWorker(JsonElement list, string productPath, string productHash,
        string runtimePath, string runtimeHash, DateTimeOffset start, DateTimeOffset end, int requiredWorkers)
    {
        if (list.ValueKind != JsonValueKind.Array || requiredWorkers < 1) return false;
        var observations = list.EnumerateArray().ToArray();
        var brokers = observations.Where(x => OptionalText(x, "Kind")?.Equals("broker", StringComparison.OrdinalIgnoreCase) == true &&
            Text(x, "Sha256").Equals(productHash, StringComparison.OrdinalIgnoreCase) && LocalPath(Text(x, "ImagePath")).Equals(productPath, StringComparison.OrdinalIgnoreCase) &&
            PositiveInt(x, "ProcessId") && InWindow(x, start, end)).ToArray();
        var workers = new HashSet<(int Pid, DateTimeOffset Start)>();
        foreach (var observation in observations.Where(x => OptionalText(x, "Kind")?.Equals("worker", StringComparison.OrdinalIgnoreCase) == true))
        {
            if (!PositiveInt(observation, "ProcessId") || !PositiveInt(observation, "ParentProcessId") ||
                !True(observation, "TokenQuerySucceeded") || !True(observation, "IsAppContainer") || !Null(observation, "QueryError") ||
                !Text(observation, "Sha256").Equals(runtimeHash, StringComparison.OrdinalIgnoreCase) ||
                !LocalPath(Text(observation, "ImagePath")).Equals(runtimePath, StringComparison.OrdinalIgnoreCase) || !InWindow(observation, start, end)) continue;
            var parent = observation.GetProperty("ParentProcessId").GetInt32();
            if (!brokers.Any(x => x.GetProperty("ProcessId").GetInt32() == parent && Timestamp(x, "StartedUtc") <= Timestamp(observation, "StartedUtc"))) continue;
            workers.Add((observation.GetProperty("ProcessId").GetInt32(), Timestamp(observation, "StartedUtc")));
        }
        return workers.Count >= requiredWorkers;
    }

    private static bool InWindow(JsonElement observation, DateTimeOffset start, DateTimeOffset end)
    {
        var created = Timestamp(observation, "StartedUtc");
        var observed = Timestamp(observation, "ObservedUtc");
        return created >= start.AddSeconds(-2) && observed >= created && observed <= end.AddSeconds(2);
    }

    private static Dictionary<string, string> ReadBundle(string directory, List<EvidenceFileDigest> digests)
    {
        var root = LocalPath(directory);
        if (root.Equals(Path.GetPathRoot(root), StringComparison.OrdinalIgnoreCase)) throw new IOException("A dedicated evidence directory is required.");
        RejectReparse(root);
        if (!Directory.Exists(root)) throw new IOException("Evidence directory is unavailable.");
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>(); pending.Push(root);
        long total = 0; var count = 0;
        while (pending.TryPop(out var current))
            foreach (var path in Directory.EnumerateFileSystemEntries(current))
            {
                if (++count > 1024) throw new IOException("Evidence bundle exceeds its entry budget.");
                RejectReparse(path);
                if (Directory.Exists(path)) { pending.Push(path); continue; }
                var extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension is not (".json" or ".jsonl")) continue;
                var name = Path.GetFileNameWithoutExtension(path);
                if (name.Contains("verification", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Equals("auth", StringComparison.OrdinalIgnoreCase)) throw new IOException("Unexpected credential file.");
                using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var bytes = new MemoryStream();
                var buffer = new byte[8192]; int length;
                while ((length = file.Read(buffer)) != 0)
                {
                    if (bytes.Length + length > MaximumFileBytes || (total += length) > MaximumBundleBytes) throw new IOException("Evidence exceeds its byte budget.");
                    bytes.Write(buffer, 0, length);
                }
                var data = bytes.ToArray();
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                result.Add(relative, new UTF8Encoding(false, true).GetString(data));
                digests.Add(new(relative, data.Length, Convert.ToHexStringLower(SHA256.HashData(data))));
            }
        digests.Sort((a, b) => StringComparer.Ordinal.Compare(a.File, b.File));
        return result;
    }

    private static string Required(Dictionary<string, string> files, string name) => files.TryGetValue(name, out var text) ? text : throw new IOException("Required raw evidence is absent.");
    private static JsonElement Parse(string text)
    {
        using var json = JsonDocument.Parse(text, JsonOptions);
        var budget = 65536; ValidateUnique(json.RootElement, ref budget);
        return json.RootElement.Clone();
    }
    private static List<JsonElement> Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(x => !string.IsNullOrWhiteSpace(x)).Select(Parse).ToList();
    private static void ValidateUnique(JsonElement value, ref int budget)
    {
        if (--budget < 0) throw new JsonException("Evidence exceeds its JSON budget.");
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject()) { if (!names.Add(property.Name)) throw new JsonException("Duplicate evidence field."); ValidateUnique(property.Value, ref budget); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var child in value.EnumerateArray()) ValidateUnique(child, ref budget);
    }
    private static JsonElement OneStage(IReadOnlyList<JsonElement> log, string name) => log.Single(x => OptionalText(x, "stage") == name);
    private static JsonElement OneCaseStage(IReadOnlyList<JsonElement> log, string name, string id) => log.Single(x => OptionalText(x, "stage") == name && OptionalText(x.GetProperty("details"), "id") == id);
    private static string Text(JsonElement value, string name) => OptionalText(value, name) ?? throw new JsonException("Required string is absent.");
    private static string? OptionalText(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static bool True(JsonElement value, string name) => value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.True;
    private static bool False(JsonElement value, string name) => value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.False;
    private static bool Null(JsonElement value, string name) => value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.Null;
    private static bool PositiveInt(JsonElement value, string name) => value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var number) && number > 0;
    private static decimal? Decimal(JsonElement value) => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) ? number : value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    private static DateTimeOffset Timestamp(JsonElement value, string name) => DateTimeOffset.Parse(Text(value, name), CultureInfo.InvariantCulture, DateTimeStyles.None);
    private static bool ValidHash(string value) => value.Length == 64 && value.All(char.IsAsciiHexDigit);
    private static bool HasStorageSuffix(string ownedRoot, string applicationDirectory) => ownedRoot.EndsWith(
        Path.DirectorySeparatorChar + Path.Combine(applicationDirectory, "ManagedAi", "skill-execution"), StringComparison.OrdinalIgnoreCase);
    private static string LocalPath(string value)
    {
        if (!Path.IsPathFullyQualified(value) || value.StartsWith("\\\\", StringComparison.Ordinal) || value.AsSpan(2).Contains(':')) throw new IOException("Only local absolute paths are accepted.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
    }
    private static bool Under(string root, string path) => path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static void RejectReparse(string value)
    {
        for (var path = Path.GetFullPath(value); !string.IsNullOrEmpty(path); path = Path.GetDirectoryName(path))
            if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse evidence paths are not accepted.");
    }
    private static bool Expected(Exception error) => error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or JsonException or KeyNotFoundException or FormatException;
}
