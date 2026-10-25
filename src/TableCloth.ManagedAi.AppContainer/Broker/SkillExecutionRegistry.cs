using System.Text;
using System.Text.Json;

namespace TableCloth.AppContainerPoc;

public sealed record SkillExecutionResult(string Status, int? ExitCode, string Stdout, string Stderr,
    bool WasAppContainer, bool TimedOut, bool OutputLimited, string? Failure)
{
    public bool IsError => Status != "completed";
}

public sealed class SkillExecutionRegistry
{
    private readonly EvaluationManifest _manifest;
    private readonly Func<SandboxRunRequest, CancellationToken, Task<SandboxRunResult>> _execute;
    private readonly SemaphoreSlim _operation = new(1, 1);
    public IReadOnlyList<SkillRegistration> Skills => _manifest.Skills;

    private SkillExecutionRegistry(EvaluationManifest manifest,
        Func<SandboxRunRequest, CancellationToken, Task<SandboxRunResult>>? execute)
    {
        _manifest = manifest;
        _execute = execute ?? new AppContainerProcessRunner(manifest.OwnedRoot).RunAsync;
    }

    public static Task<SkillExecutionRegistry> LoadAsync(string manifestPath, CancellationToken token)
        => LoadAsync(manifestPath, null, token);

    // Executor injection is an offline test seam, not part of MCP input or production configuration.
    public static async Task<SkillExecutionRegistry> LoadAsync(string manifestPath,
        Func<SandboxRunRequest, CancellationToken, Task<SandboxRunResult>>? execute, CancellationToken token)
        => new(await BrokerFiles.ReadManifestAsync(manifestPath, token), execute);

    public async Task<SkillExecutionResult> ExecuteAsync(string skillId, JsonElement input, CancellationToken token)
    {
        var skill = Skills.FirstOrDefault(x => string.Equals(x.SkillId, skillId, StringComparison.Ordinal));
        if (skill is null) return Blocked("unknown-skill");
        try { ValidateInput(skill, input); }
        catch (Exception error) when (error is BrokerValidationException or InvalidOperationException)
        { return Blocked("invalid-input"); }
        // Internet capability is never granted by this broker, including trusted fixed CLI registrations.
        if (skill.RequiresNetwork) return Blocked("network-policy");
        await _operation.WaitAsync(token);
        string? work = null;
        SkillExecutionResult result = Blocked("worker-unavailable");
        var cleanupFailed = false;
        try
        {
            var executable = skill.Kind == "node-json-stdin" ? _manifest.NodeExecutable! : _manifest.PythonExecutable;
            var executableHash = skill.Kind == "node-json-stdin" ? _manifest.NodeSha256! : _manifest.PythonSha256;
            await using var executableLock = await BrokerFiles.OpenVerifiedAsync(_manifest.OwnedRoot, executable, executableHash, token);
            await using var scriptLock = await BrokerFiles.OpenVerifiedAsync(_manifest.OwnedRoot, skill.ScriptPath, skill.Sha256, token);
            await using var runtimeLocks = await BrokerFiles.OpenRuntimeFilesAsync(_manifest.OwnedRoot, _manifest.RuntimeFiles, token);
            var workRoot = Path.Combine(_manifest.OwnedRoot, "broker-work");
            BrokerFiles.RequireUnder(_manifest.OwnedRoot, workRoot);
            BrokerFiles.RejectReparsePoints(workRoot);
            Directory.CreateDirectory(workRoot);
            BrokerFiles.RejectReparsePoints(workRoot);
            work = Path.Combine(workRoot, Guid.NewGuid().ToString("N"));
            BrokerFiles.RequireUnder(_manifest.OwnedRoot, work);
            Directory.CreateDirectory(work);
            BrokerFiles.RejectReparsePoints(work);
            var arguments = new List<string>();
            if (skill.Kind == "node-json-stdin") arguments.Add("--preserve-symlinks-main");
            else arguments.AddRange(["-I", "-S", "-B", "-X", "utf8"]);
            arguments.Add(skill.ScriptPath);
            if (skill.Kind == "python-cli-fixed") arguments.AddRange(skill.FixedArguments);
            var request = new SandboxRunRequest(executable, arguments.AsReadOnly(), work,
                skill.Kind == "python-cli-fixed" ? "" : input.GetRawText() + "\n",
                Array.AsReadOnly(new[] { Path.GetDirectoryName(executable)!, Path.GetDirectoryName(skill.ScriptPath)! }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()),
                Array.AsReadOnly(new[] { work }), AllowNetwork: false);
            var outcome = await _execute(request, token);
            var outputSize = Encoding.UTF8.GetByteCount(outcome.Stdout) + (long)Encoding.UTF8.GetByteCount(outcome.Stderr);
            if (outcome.OutputLimited || outputSize > request.MaxOutputBytes)
                result = new("failed", outcome.ExitCode, "", "", outcome.WasAppContainer, outcome.TimedOut, true, "output-limit");
            else if (!outcome.WasAppContainer)
                result = new("blocked", outcome.ExitCode, "", "", false, outcome.TimedOut, false, "appcontainer-required");
            else
            {
                var completed = outcome.ExitCode == 0 && !outcome.TimedOut && outcome.Failure is null;
                var failure = outcome.TimedOut ? "timeout" : outcome.Failure is not null ? "worker-failure" : outcome.ExitCode != 0 ? "worker-exit" : null;
                if (completed && skill.Kind == "python-json-stdin" && !ValidCalculatorOutput(outcome.Stdout))
                { completed = false; failure = "invalid-worker-output"; }
                result = new(completed ? "completed" : "failed", outcome.ExitCode, outcome.Stdout, outcome.Stderr,
                    true, outcome.TimedOut, false, failure);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (BrokerValidationException) { result = Blocked("registration-integrity"); }
        catch { result = Blocked("worker-unavailable"); }
        finally
        {
            try { if (work is not null) DeleteWorkDirectory(work); }
            catch { cleanupFailed = true; }
            finally { _operation.Release(); }
        }
        return cleanupFailed ? result with { Status = "failed", Failure = "cleanup-failed" } : result;
    }

    private static void ValidateInput(SkillRegistration skill, JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object || Encoding.UTF8.GetByteCount(input.GetRawText()) > BrokerFiles.MaximumInputBytes) throw BrokerFiles.Invalid();
        BrokerFiles.ValidateJson(input);
        if (skill.Kind == "python-cli-fixed")
        {
            if (input.EnumerateObject().Any()) throw BrokerFiles.Invalid();
        }
        else if (skill.Kind == "python-json-stdin")
        {
            var operation = BrokerFiles.Text(input, "operation", 64);
            if (!skill.AllowedOperations.Contains(operation, StringComparer.Ordinal)) throw BrokerFiles.Invalid();
            BrokerFiles.ValidateCalculatorInput(input, operation);
        }
        else
        {
            BrokerFiles.ObjectFields(input, ["a", "b"]);
            foreach (var field in new[] { "a", "b" })
                if (!input.TryGetProperty(field, out var number) || number.ValueKind != JsonValueKind.Number ||
                    !number.TryGetDouble(out var parsed) || !double.IsFinite(parsed)) throw BrokerFiles.Invalid();
        }
    }

    private static bool ValidCalculatorOutput(string text)
    {
        try
        {
            using var result = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 32 });
            BrokerFiles.ValidateJson(result.RootElement);
            return result.RootElement.ValueKind == JsonValueKind.Object && result.RootElement.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True;
        }
        catch (Exception error) when (error is JsonException or BrokerValidationException) { return false; }
    }

    private void DeleteWorkDirectory(string directory)
    {
        // A worker cannot turn cleanup into a recursive operation on an unowned/reparse target.
        BrokerFiles.RequireUnder(_manifest.OwnedRoot, directory);
        BrokerFiles.RejectReparsePoints(directory);
        if (!Directory.Exists(directory)) return;
        var pending = new Stack<string>();
        pending.Push(directory);
        var count = 0;
        while (pending.TryPop(out var current))
        {
            BrokerFiles.RejectReparsePoints(current);
            foreach (var child in Directory.EnumerateFileSystemEntries(current))
            {
                if (++count > 4096) throw BrokerFiles.Invalid();
                BrokerFiles.RequireUnder(directory, child);
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) throw BrokerFiles.Invalid();
                if (Directory.Exists(child)) pending.Push(child);
            }
        }
        Directory.Delete(directory, recursive: true);
    }

    private static SkillExecutionResult Blocked(string reason) => new("blocked", null, "", "", false, false, false, reason);
}
