using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TableCloth.ManagedAi;

public interface IManagedAiCertificateBridge
{
    Task<string> GetExpiryReportAsync(CancellationToken cancellationToken);
}

public static class CertificateExpiryIntent
{
    public static bool Matches(string message)
    {
        var certificate = message.Contains("인증서", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("certificate", StringComparison.OrdinalIgnoreCase);
        var expiry = message.Contains("만료", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("유효기간", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("갱신", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("expir", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("renew", StringComparison.OrdinalIgnoreCase);
        return certificate && expiry;
    }
}

public sealed class ManagedAiCertificateBridge(IJsonlProcessRunner runner, string? executablePath = null) : IManagedAiCertificateBridge
{
    public async Task<string> GetExpiryReportAsync(CancellationToken cancellationToken)
    {
        var executable = executablePath ?? Path.Combine(AppContext.BaseDirectory, "TableClothCli.exe");
        if (!File.Exists(executable)) throw new ManagedAiException(AiFailureCode.CertificateScanUnavailable);
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false, true),
            WorkingDirectory = AppContext.BaseDirectory
        };
        start.ArgumentList.Add("certificates");
        start.ArgumentList.Add("expiring");
        start.ArgumentList.Add("--within-days");
        start.ArgumentList.Add("30");
        start.ArgumentList.Add("--with-catalog-summary");
        string? report = null;
        var outcome = await runner.RunAsync(new(start, null, TimeSpan.FromSeconds(20), 64 * 1024, 1024, 64 * 1024),
            line =>
            {
                if (report is not null) throw new ManagedAiException(AiFailureCode.CertificateScanUnavailable);
                report = line;
            }, null, cancellationToken);
        if (outcome.ExitCode is not (0 or 3) || report is null)
            throw new ManagedAiException(AiFailureCode.CertificateScanUnavailable);
        try
        {
            using var parsed = JsonDocument.Parse(report, new JsonDocumentOptions { MaxDepth = 8 });
            var root = parsed.RootElement;
            foreach (var property in root.EnumerateObject())
                if (property.Name is not ("rootFound" or "scannedAt" or "withinDays" or "pairCount" or
                    "skippedDirectories" or "unreadableCertificates" or "certificates" or "catalog"))
                    throw new JsonException();
            if (root.GetProperty("withinDays").GetInt32() != 30 ||
                root.GetProperty("pairCount").GetInt32() is < 0 or > 200 ||
                root.GetProperty("skippedDirectories").GetInt32() is < 0 or > 2000 ||
                root.GetProperty("unreadableCertificates").GetInt32() is < 0 or > 200 ||
                root.GetProperty("rootFound").ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                root.GetProperty("certificates").GetArrayLength() > 200)
                throw new JsonException();
            foreach (var item in root.GetProperty("certificates").EnumerateArray())
            {
                foreach (var property in item.EnumerateObject())
                    if (property.Name is not ("number" or "expiresAt" or "daysRemaining" or "status"))
                        throw new JsonException();
                if (item.GetProperty("number").GetInt32() is < 1 or > 200 ||
                    item.GetProperty("daysRemaining").GetInt32() is < -100000 or > 30 ||
                    item.GetProperty("status").GetString() is not ("expired" or "expiring") ||
                    !item.GetProperty("expiresAt").TryGetDateTimeOffset(out _)) throw new JsonException();
            }
            var catalog = root.GetProperty("catalog");
            foreach (var property in catalog.EnumerateObject())
                if (property.Name is not ("cacheFound" or "serviceCount" or "snapshotModifiedAt"))
                    throw new JsonException();
            if (catalog.GetProperty("cacheFound").ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                catalog.GetProperty("serviceCount").GetInt32() is < 0 or > 100000)
                throw new JsonException();
            if (!root.GetProperty("scannedAt").TryGetDateTimeOffset(out _)) throw new JsonException();
            return report;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        { throw new ManagedAiException(AiFailureCode.CertificateScanUnavailable); }
    }
}
