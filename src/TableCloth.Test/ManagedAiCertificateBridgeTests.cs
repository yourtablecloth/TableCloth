using TableCloth.ManagedAi;
using TableCloth.ManagedAi.OpenAi;

namespace TableCloth.Test;

[TestClass]
public sealed class ManagedAiCertificateBridgeTests
{
    private const string SafeReport = """
        {"rootFound":true,"scannedAt":"2026-09-25T13:00:00+09:00","withinDays":30,
         "pairCount":2,"skippedDirectories":0,"unreadableCertificates":0,
         "certificates":[{"number":1,"expiresAt":"2026-09-20T00:00:00+09:00","daysRemaining":-6,"status":"expired"},
                         {"number":2,"expiresAt":"2026-10-05T00:00:00+09:00","daysRemaining":10,"status":"expiring"}]}
        """;

    [TestMethod]
    public async Task BridgeInvokesOnlyTheFixedReadOnlyCommandAndAcceptsMinimalReport()
    {
        var runner = new Runner(SafeReport);
        var bridge = new ManagedAiCertificateBridge(runner, Path.Combine(AppContext.BaseDirectory, "TableClothCli.exe"));
        var counts = await bridge.GetExpiryReportAsync(CancellationToken.None);
        Assert.AreEqual("{\"rootFound\":true,\"withinDays\":30,\"expiredCount\":1,\"expiringCount\":1,\"scanIncomplete\":false}", counts);
        Assert.DoesNotContain("expiresAt", counts);
        Assert.DoesNotContain("catalog", counts);
        CollectionAssert.AreEqual(new[] { "certificates", "expiring", "--within-days", "30" },
            runner.Spec!.StartInfo.ArgumentList.ToArray());
        Assert.IsNull(runner.Spec.Input);
        Assert.IsTrue(CertificateExpiryIntent.Matches("공동인증서 만료일을 알려 주세요"));
        Assert.IsFalse(CertificateExpiryIntent.Matches("인증서를 설치해 주세요"));
        var prompt = OpenAiChatPromptFactory.Create(new("공동인증서 만료일", [], null, counts));
        Assert.Contains("LOCAL_CERTIFICATE_EXPIRY_REPORT", prompt);
        Assert.DoesNotContain("expiresAt", prompt);
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

    [TestMethod]
    public void CertificateInputGuardRejectsDetailsAndScreenshotLinksBeforePromptCreation()
    {
        foreach (var input in new[]
        {
            "인증서 주체명: 홍길동",
            "-----BEGIN CERTIFICATE-----\nMII...",
            "인증서 스크린샷 https://example.com/screenshot",
            "인증서 사진 https://example.com/cert.png",
            "https://example.com/unlabeled.png"
        })
        {
            Assert.IsTrue(CertificateInputGuard.ContainsProhibitedMaterial(input), input);
            var error = Assert.ThrowsExactly<ManagedAiException>(() => OpenAiChatPromptFactory.Create(new(input, [], null)));
            Assert.AreEqual(AiFailureCode.InvalidQuery, error.Code);
        }
        Assert.IsFalse(CertificateInputGuard.ContainsProhibitedMaterial("인증서가 몇 개 만료되었나요?"));
        Assert.IsFalse(CertificateInputGuard.ContainsProhibitedMaterial("인증서의 발급자는 어디서 확인하나요?"));
    }

    [TestMethod]
    public void PromptRejectsPerCertificateReportEvenIfBridgeIsBypassed()
    {
        var error = Assert.ThrowsExactly<ManagedAiException>(() =>
            OpenAiChatPromptFactory.Create(new("인증서 만료 수", [], null, SafeReport)));
        Assert.AreEqual(AiFailureCode.InvalidQuery, error.Code);
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
