using System.Diagnostics;
using System.Text;

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
            ["--search", "--ask-for-approval", "never", "--strict-config", "exec", "--json",
             "--output-schema", schemaPath, "--sandbox", "read-only", "--skip-git-repo-check",
             "--ephemeral", "--color", "never", "-C", runDirectory, "-"], options.HardTimeout);
        return spec with { Input = prompt, MaxStdoutBytes = options.MaxStdoutBytes,
            MaxStderrBytes = options.MaxStderrBytes, MaxLineBytes = options.MaxLineBytes };
    }

    public static ProcessRunSpec Chat(ManagedRuntime runtime, string runDirectory, string prompt, ManagedAiOptions options, string? model = null)
    {
        var spec = Command(runtime, runDirectory,
            ["--search", "--ask-for-approval", "never", "--strict-config", "exec", "--json",
             "--sandbox", "read-only", "--skip-git-repo-check", "--ephemeral", "--color", "never", "-C", runDirectory, "-"],
            options.HardTimeout);
        if (model is not null)
        {
            AiModel.ValidateId(model);
            // Insert before the final stdin prompt marker.
            spec.StartInfo.ArgumentList.Insert(0, model);
            spec.StartInfo.ArgumentList.Insert(0, "--model");
        }
        return spec with { Input = prompt, MaxStdoutBytes = options.MaxStdoutBytes,
            MaxStderrBytes = options.MaxStderrBytes, MaxLineBytes = options.MaxLineBytes };
    }
}
