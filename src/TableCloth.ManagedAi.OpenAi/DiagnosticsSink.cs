using System.Text.Json;
using System.Text.Json.Serialization;

namespace TableCloth.ManagedAi.OpenAi;

public sealed record DiagnosticRecord(DateTimeOffset Timestamp, Guid RunId, string Version,
    long DurationMs, int Events, int SearchCalls, long StdoutBytes, long StderrBytes, string Outcome);

[JsonSerializable(typeof(DiagnosticRecord))]
internal partial class DiagnosticJsonContext : JsonSerializerContext;

public sealed class DiagnosticsSink(ManagedAiPaths paths)
{
    public void Write(Guid runId, string version, long durationMs, int events, int searchCalls,
        long stdoutBytes, long stderrBytes, AiFailureCode? failure, bool cancelled)
    {
        // No exception objects, user content, provider strings, arguments or environment dictionaries accepted here.
        try
        {
            OpenAiReleaseMetadataClient.ValidateVersion(version);
            Directory.CreateDirectory(paths.Logs);
            var files = new DirectoryInfo(paths.Logs).GetFiles("diagnostic-*.jsonl").OrderBy(x => x.LastWriteTimeUtc).ToList();
            long total = files.Sum(x => x.Length);
            foreach (var file in files)
            {
                ManagedAiPaths.RejectReparsePoints(file.FullName);
                if (file.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-7) || total > 15L * 1024 * 1024)
                { total -= file.Length; file.Delete(); }
            }
            var path = paths.Under("logs", $"diagnostic-{DateTime.UtcNow:yyyyMMdd}.jsonl");
            if (File.Exists(path) && new FileInfo(path).Length > 5 * 1024 * 1024 - 4096) return;
            var record = new DiagnosticRecord(DateTimeOffset.UtcNow, runId, version, durationMs, events,
                searchCalls, stdoutBytes, stderrBytes, cancelled ? "Cancelled" : failure?.ToString() ?? "Completed");
            File.AppendAllText(path, JsonSerializer.Serialize(record, DiagnosticJsonContext.Default.DiagnosticRecord) + "\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ManagedAiException)
        { /* Diagnostics must never replace the provider's outcome. */ }
    }
}
