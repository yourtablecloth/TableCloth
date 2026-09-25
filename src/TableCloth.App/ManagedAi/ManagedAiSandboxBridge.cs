using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TableCloth.ManagedAi;

public enum SandboxCliAction { List, Start, Stop, Connect, Ip }

public sealed record SandboxCliRequest(SandboxCliAction Action, Guid? Id = null);

public static class WindowsSandboxIntent
{
    public static SandboxCliRequest? Parse(string message)
    {
        message = message.Replace("wsb.exe", "wsb", StringComparison.OrdinalIgnoreCase);
        if (!Contains(message, "샌드박스") && !Contains(message, "sandbox") && !Contains(message, "wsb")) return null;
        if (Contains(message, "명령 실행") || Contains(message, "폴더 공유") || Contains(message, "wsb exec") ||
            Contains(message, "wsb share")) return null;

        var informational = Contains(message, "방법") || Contains(message, "어떻게") || Contains(message, "설명") ||
            Contains(message, "가능") || Contains(message, "알려") || message.TrimEnd().EndsWith('?');

        SandboxCliAction? action = null;
        if (!informational && (Contains(message, "종료해") || Contains(message, "중지해") ||
            Contains(message, "wsb stop"))) action = SandboxCliAction.Stop;
        else if (!informational && (Contains(message, "연결해") || Contains(message, "접속해") ||
                 Contains(message, "wsb connect"))) action = SandboxCliAction.Connect;
        else if (Contains(message, "ip 주소") || Contains(message, "ip주소") || Contains(message, "아이피") ||
                 Contains(message, "wsb ip")) action = SandboxCliAction.Ip;
        else if (!informational && (Contains(message, "샌드박스 시작해") || Contains(message, "샌드박스를 시작해") ||
                 Contains(message, "샌드박스 실행해") || Contains(message, "샌드박스를 실행해") ||
                 Contains(message, "새 샌드박스 시작") || Contains(message, "start sandbox") ||
                 Contains(message, "start windows sandbox") ||
                 Contains(message, "wsb start"))) action = SandboxCliAction.Start;
        else if (Contains(message, "목록") || Contains(message, "상태") || Contains(message, "실행 중") ||
                 Contains(message, "몇 개") || Contains(message, "wsb list")) action = SandboxCliAction.List;
        if (action is null) return null;

        var match = Regex.Match(message,
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return new(action.Value, match.Success && Guid.TryParse(match.Value, out var id) ? id : null);
    }

    private static bool Contains(string text, string value) => text.Contains(value, StringComparison.OrdinalIgnoreCase);
}

public interface IManagedAiSandboxBridge
{
    Task<string> ExecuteAsync(SandboxCliRequest request, CancellationToken cancellationToken);
}

public sealed record SandboxCliOutcome(int ExitCode, string Stdout, string Stderr);

public interface ISandboxCliProcessRunner
{
    Task<SandboxCliOutcome> RunAsync(ProcessStartInfo start, CancellationToken cancellationToken);
}

public sealed class SandboxCliProcessRunner : ISandboxCliProcessRunner
{
    public async Task<SandboxCliOutcome> RunAsync(ProcessStartInfo start, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var process = new Process { StartInfo = start };
        var started = false;
        try
        {
            if (!process.Start()) throw new Win32Exception();
            started = true;
            var stdout = ReadBoundedAsync(process.StandardOutput, 12000, linked.Token);
            var stderr = ReadBoundedAsync(process.StandardError, 4000, linked.Token);
            await process.WaitForExitAsync(linked.Token);
            return new(process.ExitCode, await stdout, await stderr);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        { throw new ManagedAiException(AiFailureCode.ProviderTimeout); }
        finally
        {
            // wsb may open a persistent Sandbox UI. Terminate only the CLI on cancellation.
            if (started)
                try { if (!process.HasExited) process.Kill(entireProcessTree: false); }
                catch (InvalidOperationException) { }
                catch (Win32Exception) { }
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int limit, CancellationToken token)
    {
        var result = new StringBuilder();
        var buffer = new char[1024];
        int count;
        while ((count = await reader.ReadAsync(buffer, token)) > 0)
        {
            if (result.Length + count > limit) throw new ManagedAiException(AiFailureCode.OutputLimitExceeded);
            result.Append(buffer, 0, count);
        }
        return result.ToString().Trim();
    }
}

internal sealed record SandboxCliReport(string Action, string Status, int? ExitCode = null,
    string? Output = null, string? Id = null);

[JsonSerializable(typeof(SandboxCliReport))]
internal partial class SandboxCliJsonContext : JsonSerializerContext;

public sealed class ManagedAiSandboxBridge(ISandboxCliProcessRunner runner, string? executablePath = null) : IManagedAiSandboxBridge
{
    private readonly string _executable = executablePath ?? System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wsb.exe");

    public async Task<string> ExecuteAsync(SandboxCliRequest request, CancellationToken cancellationToken)
    {
        var action = request.Action.ToString().ToLowerInvariant();
        if (!Enum.IsDefined(request.Action)) throw new ArgumentOutOfRangeException(nameof(request));
        try
        {
            var id = request.Id;
            if (request.Action is SandboxCliAction.Stop or SandboxCliAction.Connect or SandboxCliAction.Ip && id is null)
            {
                var listed = await RunAsync(SandboxCliAction.List, null, cancellationToken);
                if (listed.ExitCode != 0) return Serialize(new(action, "list-failed", listed.ExitCode, listed.Output));
                var ids = ParseIds(listed.Output);
                if (ids is null) return Serialize(new(action, "unrecognized-list", listed.ExitCode, listed.Output));
                if (ids.Count != 1) return Serialize(new(action, ids.Count == 0 ? "no-running-sandbox" : "id-required",
                    listed.ExitCode, listed.Output));
                id = ids[0];
            }

            var result = await RunAsync(request.Action, id, cancellationToken);
            return Serialize(new(action, result.ExitCode == 0 ? "succeeded" : "failed", result.ExitCode,
                result.Output, id?.ToString("D")));
        }
        catch (Exception ex) when (ex is Win32Exception or System.IO.FileNotFoundException or System.IO.DirectoryNotFoundException)
        {
            return Serialize(new(action, "unavailable"));
        }
        catch (ManagedAiException ex) when (ex.Code == AiFailureCode.BlockedByPolicy)
        {
            return Serialize(new(action, "unavailable"));
        }
    }

    private async Task<(int ExitCode, string Output)> RunAsync(SandboxCliAction action, Guid? id,
        CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(_executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false, true), StandardErrorEncoding = new UTF8Encoding(false, true)
        };
        start.ArgumentList.Add(action.ToString().ToLowerInvariant());
        if (id is not null)
        {
            start.ArgumentList.Add("--id");
            start.ArgumentList.Add(id.Value.ToString("D"));
        }
        start.ArgumentList.Add("--raw");
        var outcome = await runner.RunAsync(start, cancellationToken);
        var text = outcome.ExitCode == 0 ? outcome.Stdout : outcome.Stderr.Length > 0 ? outcome.Stderr : outcome.Stdout;
        return (outcome.ExitCode, text.Length > 12000 ? text[..12000] : text);
    }

    private static IReadOnlyList<Guid>? ParseIds(string output)
    {
        try
        {
            using var document = JsonDocument.Parse(output, new JsonDocumentOptions { MaxDepth = 8 });
            if (!document.RootElement.TryGetProperty("WindowsSandboxEnvironments", out var environments) ||
                environments.ValueKind != JsonValueKind.Array) return null;
            var ids = new List<Guid>();
            foreach (var environment in environments.EnumerateArray())
            {
                string? value = null;
                if (environment.ValueKind == JsonValueKind.String) value = environment.GetString();
                else if (environment.ValueKind == JsonValueKind.Object)
                    foreach (var property in environment.EnumerateObject())
                        if ((property.Name.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                             property.Name.Equals("sandboxId", StringComparison.OrdinalIgnoreCase)) &&
                            property.Value.ValueKind == JsonValueKind.String)
                        { value = property.Value.GetString(); break; }
                if (!Guid.TryParse(value, out var id)) return null;
                ids.Add(id);
            }
            return ids;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { return null; }
    }

    private static string Serialize(SandboxCliReport report)
        => JsonSerializer.Serialize(report, SandboxCliJsonContext.Default.SandboxCliReport);
}
