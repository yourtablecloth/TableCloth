using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace TableCloth.ManagedAi.OpenAi;

public static class OpenAiCodexInvocationBuilder
{
    public static ProcessRunSpec Command(ManagedRuntime runtime, string workingDirectory, IEnumerable<string> arguments, TimeSpan timeout)
    {
        if (!Path.IsPathFullyQualified(runtime.EntryPoint) || !Path.IsPathFullyQualified(runtime.ProfileDirectory) ||
            !Path.IsPathFullyQualified(workingDirectory)) throw new ManagedAiException(AiFailureCode.InvalidPath);
        var info = new ProcessStartInfo(runtime.EntryPoint)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = new UTF8Encoding(false, true),
            StandardErrorEncoding = new UTF8Encoding(false, true), WorkingDirectory = workingDirectory
        };
        foreach (var arg in arguments) info.ArgumentList.Add(arg);
        // Environment allowlist excludes API credentials, inherited Codex sessions, MCP overrides and OTEL endpoints.
        var inherited = info.Environment.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        info.Environment.Clear();
        string[] allowed = ["SystemRoot", "WINDIR", "ComSpec", "SystemDrive", "TEMP", "TMP", "USERPROFILE",
            "LOCALAPPDATA", "APPDATA", "HOMEDRIVE", "HOMEPATH", "ProgramFiles", "ProgramFiles(x86)",
            "ProgramW6432", "PATH", "PATHEXT", "HTTPS_PROXY", "HTTP_PROXY", "NO_PROXY"];
        foreach (var name in allowed)
            if (inherited.TryGetValue(name, out var value)) info.Environment[name] = value;
        info.Environment["CODEX_HOME"] = runtime.ProfileDirectory;
        return new(info, null, timeout, 65536, 65536, 16384);
    }

    public static ProcessRunSpec Search(ManagedRuntime runtime, string runDirectory, string schemaPath, string prompt, ManagedAiOptions options)
    {
        var spec = Command(runtime, runDirectory,
            ["--profile", ManagedAiPaths.SkillProfileName, "--search", "--ask-for-approval", "never", "--strict-config", "exec", "--json",
             "--output-schema", schemaPath, "--sandbox", "read-only", "--skip-git-repo-check",
             "--ephemeral", "--color", "never", "-C", runDirectory, "-"], options.HardTimeout);
        return spec with { Input = prompt, MaxStdoutBytes = options.MaxStdoutBytes,
            MaxStderrBytes = options.MaxStderrBytes, MaxLineBytes = options.MaxLineBytes };
    }

    public static ProcessRunSpec Chat(ManagedRuntime runtime, string runDirectory, string prompt, ManagedAiOptions options, string? model = null,
        AiSkillExecutionBridge? skillExecution = null, IReadOnlyList<string>? imagePaths = null)
    {
        var spec = Command(runtime, runDirectory,
            ["--profile", ManagedAiPaths.SkillProfileName, "--search", "--ask-for-approval", "never", "--strict-config", "exec", "--json",
             "--sandbox", "read-only", "--skip-git-repo-check", "--ephemeral", "--color", "never", "-C", runDirectory, "-"],
            options.HardTimeout);
        if (model is not null)
        {
            AiModel.ValidateId(model);
            // Insert before the final stdin prompt marker.
            spec.StartInfo.ArgumentList.Insert(0, model);
            spec.StartInfo.ArgumentList.Insert(0, "--model");
        }
        if (imagePaths is { Count: > 0 })
        {
            if (imagePaths.Count > 4 || imagePaths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != imagePaths.Count)
                throw new ManagedAiException(AiFailureCode.InvalidPath);
            var attachments = Path.GetFullPath(Path.Combine(runDirectory, "attachments"));
            foreach (var path in imagePaths)
            {
                if (!Path.IsPathFullyQualified(path)) throw new ManagedAiException(AiFailureCode.InvalidPath);
                var full = Path.GetFullPath(path);
                var name = Path.GetFileName(full);
                if (full.Contains(',') || !string.Equals(Path.GetDirectoryName(full), attachments, StringComparison.OrdinalIgnoreCase) ||
                    name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '.')) ||
                    Path.GetExtension(full) is not (".png" or ".jpg") || !File.Exists(full))
                    throw new ManagedAiException(AiFailureCode.InvalidPath);
                ManagedAiPaths.RejectReparsePoints(full);
                // Each flag and path is a separate argument; the final '-' still selects stdin for text.
                spec.StartInfo.ArgumentList.Insert(spec.StartInfo.ArgumentList.Count - 1, "--image");
                spec.StartInfo.ArgumentList.Insert(spec.StartInfo.ArgumentList.Count - 1, full);
            }
        }
        if (skillExecution is not null)
        {
            if (skillExecution.SkillIds.Count is < 1 or > 128 ||
                skillExecution.SkillIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != skillExecution.SkillIds.Count ||
                skillExecution.SkillIds.Any(id => id.Length is < 1 or > 64 || !char.IsAsciiLetterOrDigit(id[0]) ||
                    id.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_'))) ||
                !Path.IsPathFullyQualified(skillExecution.Command) || !Path.IsPathFullyQualified(skillExecution.WorkingDirectory) ||
                skillExecution.Arguments.Count != 2 || skillExecution.Arguments[0] != "--skill-mcp" ||
                !Path.IsPathFullyQualified(skillExecution.Arguments[1])) throw new ManagedAiException(AiFailureCode.BlockedByPolicy);
            static string Quoted(string value) => JsonSerializer.Serialize(value, InvocationJsonContext.Default.String);
            var registration = "mcp_servers.tablecloth_skills={command=" + Quoted(skillExecution.Command) +
                ",args=[" + string.Join(",", skillExecution.Arguments.Select(Quoted)) + "],cwd=" + Quoted(skillExecution.WorkingDirectory) +
                ",required=true,startup_timeout_sec=30,tool_timeout_sec=90,enabled_tools=[" +
                string.Join(",", skillExecution.SkillIds.Select(Quoted)) + "]," +
                "default_tools_approval_mode=\"prompt\",tools={" + string.Join(",", skillExecution.SkillIds.Select(
                    id => Quoted(id) + "={approval_mode=\"approve\"}")) + "}}";
            spec.StartInfo.ArgumentList.Insert(0, registration);
            spec.StartInfo.ArgumentList.Insert(0, "-c");
            prompt = """
                TRUSTED APPLICATION TOOL CAPABILITY:
                TableCloth has enabled the explicitly listed tablecloth_skills MCP tools for installed scripts in this session.
                When the user requests an installed skill's scripted operation, call its registered tool and base your answer on its successful result.
                This approved MCP call is permitted; arbitrary shell commands and other script execution remain prohibited.
                Each tool invokes a fixed installed Python or Node.js script inside a native Windows AppContainer. The worker has no network capability.
                Only reviewed public HTTPS GET routes can be requested through the host's bounded transport by eligible network-dependent tools.
                Generic tools take script CLI arguments in the arguments array and optional standard input in the stdin string. Read the tool description for its fixed entry point.
                Replace skill instructions invoking Python, Node.js, npx exec, or KSKILL_PYTHON with the matching registered MCP tool. Never launch an interpreter or package manager yourself.
                Public pinned instructions are supplied by the application when available. Do not execute npx instruct/files/read/update to load them.
                Instruction-only skills guide the answer without an interpreter; host certificate and Sandbox reports retain their existing application consent flow.
                Never say you used a skill or executed a script without a successful tool result. If a tool fails, report that failure and never label model arithmetic or web search as verified script execution.

                """ + "\n" + prompt;
        }
        return spec with { Input = prompt, MaxStdoutBytes = options.MaxStdoutBytes,
            MaxStderrBytes = options.MaxStderrBytes, MaxLineBytes = options.MaxLineBytes };
    }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(string))]
internal partial class InvocationJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
