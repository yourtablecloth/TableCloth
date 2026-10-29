using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace TableCloth.Models.Configuration;

/// <summary>Reads public certificate metadata from confirmed NPKI folders without opening private keys.</summary>
public sealed class CertificateExpiryScanner
{
    private const int MaxDirectories = 2000;
    private const int MaxCertificates = 200;
    private const long MaxCertificateBytes = 256 * 1024;

    public CertificateExpiryReport Scan(string root, DateTimeOffset now, int withinDays, bool includeDetails = false)
    {
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root) || withinDays < 1 || withinDays > 365)
            throw new ArgumentException("Invalid certificate scan options.");

        var report = new CertificateExpiryReport { ScannedAt = now, WithinDays = withinDays };
        if (!Directory.Exists(root)) return report;
        report.RootFound = true;
        for (var ancestor = new DirectoryInfo(Path.GetFullPath(root)); ancestor != null; ancestor = ancestor.Parent)
            if (IsReparsePoint(ancestor.FullName)) throw new IOException("Certificate root crosses a reparse point.");

        var pending = new Stack<Tuple<string, int>>();
        pending.Push(Tuple.Create(Path.GetFullPath(root), 0));
        var found = new List<CertificateExpiryItem>();
        var visited = 0;
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (++visited > MaxDirectories) throw new IOException("Certificate scan directory limit exceeded.");
            if (IsReparsePoint(current.Item1)) { report.SkippedDirectories++; continue; }
            try
            {
                var der = Path.Combine(current.Item1, "signCert.der");
                var key = Path.Combine(current.Item1, "signPri.key");
                if (File.Exists(der) && File.Exists(key) && !IsReparsePoint(der) && !IsReparsePoint(key))
                {
                    if (++report.PairCount > MaxCertificates) throw new IOException("Certificate scan file limit exceeded.");
                    var info = new FileInfo(der);
                    if (info.Length < 1 || info.Length > MaxCertificateBytes)
                    {
                        report.UnreadableCertificates++;
                    }
                    else
                    {
                        try
                        {
                            using (var certificate = new X509Certificate2(File.ReadAllBytes(der)))
                            {
                                var expiry = new DateTimeOffset(certificate.NotAfter);
                                if (expiry <= now.AddDays(withinDays))
                                    found.Add(new CertificateExpiryItem
                                    {
                                        ExpiresAt = expiry,
                                        DaysRemaining = (int)Math.Ceiling((expiry - now).TotalDays),
                                        Status = expiry <= now ? "expired" : "expiring",
                                        Subject = includeDetails ? certificate.GetNameInfo(X509NameType.SimpleName, false) : null,
                                        Directory = includeDetails ? current.Item1 : null
                                    });
                            }
                        }
                        catch (System.Security.Cryptography.CryptographicException) { report.UnreadableCertificates++; }
                    }
                }
                if (current.Item2 >= 16) { report.SkippedDirectories++; continue; }
                foreach (var child in Directory.EnumerateDirectories(current.Item1))
                    pending.Push(Tuple.Create(child, current.Item2 + 1));
            }
            catch (UnauthorizedAccessException) { report.SkippedDirectories++; }
            catch (PathTooLongException) { report.SkippedDirectories++; }
        }

        report.Certificates = found.OrderBy(x => x.ExpiresAt).ThenBy(x => x.Subject, StringComparer.Ordinal)
            .Select((item, index) => { item.Number = index + 1; return item; }).ToArray();
        return report;
    }

    private static bool IsReparsePoint(string path)
        => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
}

public sealed class CertificateExpiryReport
{
    public bool RootFound { get; set; }
    public DateTimeOffset ScannedAt { get; set; }
    public int WithinDays { get; set; }
    public int PairCount { get; set; }
    public int SkippedDirectories { get; set; }
    public int UnreadableCertificates { get; set; }
    public CertificateExpiryItem[] Certificates { get; set; } = new CertificateExpiryItem[0];
    public CertificateCatalogSummary Catalog { get; set; }
}

public sealed class CertificateCatalogSummary
{
    public bool CacheFound { get; set; }
    public int ServiceCount { get; set; }
    public DateTimeOffset? SnapshotModifiedAt { get; set; }
}

public sealed class CertificateExpiryItem
{
    public int Number { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int DaysRemaining { get; set; }
    public string Status { get; set; }
    public string Subject { get; set; }
    public string Directory { get; set; }
}
