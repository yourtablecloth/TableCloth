using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Diagnostics;
using System.Text.Json;
using TableCloth.Models.Configuration;

namespace TableCloth.Test;

[TestClass]
public sealed class CertificateExpiryScannerTests
{
    [TestMethod]
    public void ReadsOnlyCertificateMetadataAndAppliesTheExpiryWindow()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "certificate-scan-" + Guid.NewGuid().ToString("N"));
        var now = DateTimeOffset.Now;
        try
        {
            WritePair(Path.Combine(root, "soon"), now.AddDays(10));
            WritePair(Path.Combine(root, "later"), now.AddDays(90));

            var report = new CertificateExpiryScanner().Scan(root, now, 30);
            Assert.IsTrue(report.RootFound);
            Assert.AreEqual(2, report.PairCount);
            Assert.HasCount(1, report.Certificates);
            Assert.AreEqual("expiring", report.Certificates[0].Status);
            Assert.IsNull(report.Certificates[0].Subject);
            Assert.IsNull(report.Certificates[0].Directory);
            Assert.AreEqual("not-a-private-key", File.ReadAllText(Path.Combine(root, "soon", "signPri.key")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public void MissingRootIsNotReportedAsAnEmptyExistingStore()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "missing-certificate-scan-" + Guid.NewGuid().ToString("N"));
        var report = new CertificateExpiryScanner().Scan(root, DateTimeOffset.Now, 30);
        Assert.IsFalse(report.RootFound);
        Assert.HasCount(0, report.Certificates);
    }

    [TestMethod]
    public void ExpiredCertificateAndUnreadablePairRemainDistinct()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "partial-certificate-scan-" + Guid.NewGuid().ToString("N"));
        try
        {
            WritePair(Path.Combine(root, "expired"), DateTimeOffset.Now.AddDays(-1));
            var broken = Path.Combine(root, "broken");
            Directory.CreateDirectory(broken);
            File.WriteAllText(Path.Combine(broken, "signCert.der"), "invalid DER");
            File.WriteAllText(Path.Combine(broken, "signPri.key"), "not-a-private-key");
            var report = new CertificateExpiryScanner().Scan(root, DateTimeOffset.Now, 30);
            Assert.AreEqual(2, report.PairCount);
            Assert.AreEqual(1, report.UnreadableCertificates);
            Assert.HasCount(1, report.Certificates);
            Assert.AreEqual("expired", report.Certificates[0].Status);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public void CliReturnsMinimalJsonWithoutCertificateIdentity()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "certificate-cli-" + Guid.NewGuid().ToString("N"));
        try
        {
            WritePair(Path.Combine(root, "soon"), DateTimeOffset.Now.AddDays(10));
            var (exitCode, output) = RunCli("certificates", "expiring", "--within-days", "30", "--root", root);
            Assert.AreEqual(0, exitCode);
            Assert.DoesNotContain("Private Fixture Name", output);
            Assert.DoesNotContain("signPri.key", output);
            Assert.DoesNotContain(root, output);
            using var json = JsonDocument.Parse(output);
            Assert.IsTrue(json.RootElement.GetProperty("rootFound").GetBoolean());
            Assert.AreEqual(1, json.RootElement.GetProperty("certificates").GetArrayLength());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public void CliReadsCatalogCacheWithoutInventingCertificateRelations()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "catalog-cli-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            File.WriteAllText(file, "<Catalog><InternetServices>" +
                "<Service Id='Example' DisplayName='예시 은행' Category='Banking' Url='https://example.com/'></Service>" +
                "</InternetServices></Catalog>");
            var (exitCode, output) = RunCli("catalog", "services", "--query", "예시", "--file", file);
            Assert.AreEqual(0, exitCode);
            using var json = JsonDocument.Parse(output);
            Assert.AreEqual(1, json.RootElement.GetProperty("totalServices").GetInt32());
            var service = json.RootElement.GetProperty("services")[0];
            Assert.AreEqual("Example", service.GetProperty("id").GetString());
            Assert.IsFalse(service.TryGetProperty("certificate", out _));
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    [TestMethod]
    public void CliCombinesCertificateAndCatalogSummaryWithoutServiceGuessing()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "combined-cli-" + Guid.NewGuid().ToString("N"));
        var file = Path.Combine(AppContext.BaseDirectory, "combined-catalog-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            WritePair(Path.Combine(root, "soon"), DateTimeOffset.Now.AddDays(10));
            File.WriteAllText(file, "<Catalog><InternetServices>" +
                "<Service Id='Example' DisplayName='예시 은행' Category='Banking' Url='https://example.com/'></Service>" +
                "</InternetServices></Catalog>");
            var (_, output) = RunCli("certificates", "expiring", "--within-days", "30", "--root", root,
                "--with-catalog-summary", "--catalog-file", file);
            using var json = JsonDocument.Parse(output);
            Assert.AreEqual(1, json.RootElement.GetProperty("certificates").GetArrayLength());
            Assert.AreEqual(1, json.RootElement.GetProperty("catalog").GetProperty("serviceCount").GetInt32());
            Assert.IsFalse(json.RootElement.GetProperty("certificates")[0].TryGetProperty("serviceId", out _));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            if (File.Exists(file)) File.Delete(file);
        }
    }

    private static (int ExitCode, string Output) RunCli(params string[] arguments)
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "TableClothCli.exe");
        Assert.IsTrue(File.Exists(executable));
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(10000)) { process.Kill(entireProcessTree: true); Assert.Fail("CLI timed out."); }
        Assert.AreEqual(0, process.ExitCode, error);
        return (process.ExitCode, output);
    }

    private static void WritePair(string directory, DateTimeOffset expiresAt)
    {
        Directory.CreateDirectory(directory);
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Private Fixture Name", rsa, HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-365), expiresAt);
        File.WriteAllBytes(Path.Combine(directory, "signCert.der"), certificate.Export(X509ContentType.Cert));
        File.WriteAllText(Path.Combine(directory, "signPri.key"), "not-a-private-key");
    }
}
