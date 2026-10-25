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
        AiSkillExecutionBridge? skillExecution = null)
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
        if (skillExecution is not null)
        {
            if (skillExecution.SkillIds.Count != 1 || skillExecution.SkillIds[0] != "tablecloth-calculators" ||
                !Path.IsPathFullyQualified(skillExecution.Command) || !Path.IsPathFullyQualified(skillExecution.WorkingDirectory) ||
                skillExecution.Arguments.Count != 2 || skillExecution.Arguments[0] != "--skill-mcp" ||
                !Path.IsPathFullyQualified(skillExecution.Arguments[1])) throw new ManagedAiException(AiFailureCode.BlockedByPolicy);
            static string Quoted(string value) => JsonSerializer.Serialize(value, InvocationJsonContext.Default.String);
            var registration = "mcp_servers.tablecloth_skills={command=" + Quoted(skillExecution.Command) +
                ",args=[" + string.Join(",", skillExecution.Arguments.Select(Quoted)) + "],cwd=" + Quoted(skillExecution.WorkingDirectory) +
                ",required=true,startup_timeout_sec=30,tool_timeout_sec=60,enabled_tools=[\"tablecloth-calculators\"]," +
                "default_tools_approval_mode=\"prompt\",tools={\"tablecloth-calculators\"={approval_mode=\"approve\"}}}";
            spec.StartInfo.ArgumentList.Insert(0, registration);
            spec.StartInfo.ArgumentList.Insert(0, "-c");
            prompt = """
                TRUSTED APPLICATION TOOL CAPABILITY:
                TableCloth has enabled the tablecloth_skills/tablecloth-calculators MCP tool for this session.
                For every supported financial or everyday calculation, call this registered tool and base your answer on its successful result.
                This approved MCP call is permitted; arbitrary shell commands and other script execution remain prohibited.
                The tool invokes the reviewed installed calculator script inside a native Windows AppContainer with network disabled.
                If the skill instructions describe launching Python, use this MCP tool instead. Never launch an interpreter yourself.
                Never say you used a skill or executed a script without a successful tool result. If the tool fails or cannot support the calculation, explain the limitation without claiming a verified calculation.

                """ + "\n" + prompt;
        }
        return spec with { Input = prompt, MaxStdoutBytes = options.MaxStdoutBytes,
            MaxStderrBytes = options.MaxStderrBytes, MaxLineBytes = options.MaxLineBytes };
    }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(string))]
internal partial class InvocationJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
