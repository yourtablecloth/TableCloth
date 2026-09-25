using TableCloth.ManagedAi;
using TableCloth.ManagedAi.OpenAi;

namespace TableCloth.Test;

[TestClass]
public sealed class ManagedAiCertificateBridgeTests
{
    private const string SafeReport = """
        {"rootFound":true,"scannedAt":"2026-09-25T13:00:00+09:00","withinDays":30,
         "pairCount":1,"skippedDirectories":0,"unreadableCertificates":0,
         "certificates":[{"number":1,"expiresAt":"2026-10-05T00:00:00+09:00","daysRemaining":10,"status":"expiring"}],
         "catalog":{"cacheFound":false,"serviceCount":0}}
        """;

    [TestMethod]
    public async Task BridgeInvokesOnlyTheFixedReadOnlyCommandAndAcceptsMinimalReport()
    {
        var runner = new Runner(SafeReport);
        var bridge = new ManagedAiCertificateBridge(runner, Path.Combine(AppContext.BaseDirectory, "TableClothCli.exe"));
        Assert.AreEqual(SafeReport, await bridge.GetExpiryReportAsync(CancellationToken.None));
        CollectionAssert.AreEqual(new[] { "certificates", "expiring", "--within-days", "30", "--with-catalog-summary" },
            runner.Spec!.StartInfo.ArgumentList.ToArray());
        Assert.IsNull(runner.Spec.Input);
        Assert.IsTrue(CertificateExpiryIntent.Matches("공동인증서 만료일을 알려 주세요"));
        Assert.IsFalse(CertificateExpiryIntent.Matches("인증서를 설치해 주세요"));
        var prompt = OpenAiChatPromptFactory.Create(new("공동인증서 만료일", [], null, SafeReport));
        Assert.Contains("LOCAL_CERTIFICATE_EXPIRY_REPORT", prompt);
    }

    [TestMethod]
    public async Task BridgeRejectsPrivateIdentityFields()
    {
        var report = SafeReport.Replace("\"status\":\"expiring\"", "\"status\":\"expiring\",\"subject\":\"Private Name\"");
        var bridge = new ManagedAiCertificateBridge(new Runner(report),
            Path.Combine(AppContext.BaseDirectory, "TableClothCli.exe"));
        var error = await Assert.ThrowsExactlyAsync<ManagedAiException>(() => bridge.GetExpiryReportAsync(CancellationToken.None));
        Assert.AreEqual(AiFailureCode.CertificateScanUnavailable, error.Code);
    }

    private sealed class Runner(string report) : IJsonlProcessRunner
    {
        public ProcessRunSpec? Spec { get; private set; }
        public Task<ProcessOutcome> RunAsync(ProcessRunSpec spec, Action<string> stdout, Action<string>? stderr,
            CancellationToken cancellationToken)
        {
            Spec = spec;
            stdout(report);
            return Task.FromResult(new ProcessOutcome(0, report.Length, 0));
        }
    }
}
