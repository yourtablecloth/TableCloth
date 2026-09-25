using TableCloth.ManagedAi.OpenAi;
using TableCloth.ManagedAi.Windows;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class LoginFlowTests
{
    [TestMethod]
    public void DeviceFlowExtractsAnsiUrlAndCodeWithoutPersistingOutput()
    {
        var progress = new Updates();
        var parser = new CodexLoginOutputParser(AiLoginMethod.DeviceCode, progress);
        parser.Accept("Open \u001b[34mhttps://auth.openai.com/codex/device\u001b[0m");
        parser.Accept("Enter this code: \u001b[1mABCD-EFGHI\u001b[0m");
        Assert.AreEqual(AiLoginStage.AwaitingUser, progress.Values[^1].Stage);
        Assert.AreEqual("https://auth.openai.com/codex/device", progress.Values[^1].VerificationUri!.AbsoluteUri);
        Assert.AreEqual("ABCD-EFGHI", progress.Values[^1].UserCode);
        parser.Accept("token exchange private payload ignored");
        Assert.HasCount(2, progress.Values);
    }

    [TestMethod]
    [DataRow("https://evil.example/codex/device")]
    [DataRow("http://auth.openai.com/codex/device")]
    [DataRow("https://auth.openai.com.evil.example/codex/device")]
    [DataRow("https://user@auth.openai.com/codex/device")]
    [DataRow("https://auth.openai.com:8443/codex/device")]
    [DataRow("https://auth.openai.com/unexpected")]
    public void RejectsUntrustedLoginDestination(string uri)
    {
        var progress = new Updates();
        new CodexLoginOutputParser(AiLoginMethod.DeviceCode, progress).Accept(uri + " ABCD-EFGHI");
        Assert.HasCount(0, progress.Values);
        Assert.IsFalse(CodexLoginOutputParser.IsAllowedLoginUri(new(uri)));
    }

    [TestMethod]
    public async Task ExistingLoginCompletesWithoutStartingAnotherFlow()
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var runner = new LoginRunner { LoggedIn = true };
        var progress = new Updates();
        await Auth(fixture, runner).LoginAsync(AiLoginMethod.DeviceCode, progress, CancellationToken.None);
        Assert.HasCount(1, runner.Calls);
        CollectionAssert.AreEqual(new[] { "login", "status" }, runner.Calls[0]);
        Assert.AreEqual(AiLoginStage.Completed, progress.Values.Single().Stage);
    }

    [TestMethod]
    [DataRow(AiLoginMethod.DeviceCode)]
    [DataRow(AiLoginMethod.Browser)]
    public async Task LoginReportsProgressAndVerifiesSavedChatGptSession(AiLoginMethod method)
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var runner = new LoginRunner();
        var progress = new Updates();
        await Auth(fixture, runner).LoginAsync(method, progress, CancellationToken.None);
        Assert.HasCount(3, runner.Calls);
        CollectionAssert.AreEqual(method == AiLoginMethod.DeviceCode ? new[] { "login", "--device-auth" } : new[] { "login" }, runner.Calls[1]);
        Assert.AreEqual(AiLoginStage.Starting, progress.Values[0].Stage);
        Assert.AreEqual(AiLoginStage.Completed, progress.Values[^1].Stage);
        Assert.IsTrue(progress.Values.Any(x => x.VerificationUri is not null));
        Assert.AreEqual(method == AiLoginMethod.DeviceCode, progress.Values.Any(x => x.UserCode is not null));
    }

    [TestMethod]
    [DataRow(AiLoginMethod.Browser)]
    [DataRow(AiLoginMethod.DeviceCode)]
    public async Task SavedAuthenticationCompletesEvenWhenLoginKeepsRunning(AiLoginMethod method)
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var runner = new LoginRunner { KeepRunning = true };
        var progress = new Updates();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Auth(fixture, runner).LoginAsync(method, progress, deadline.Token);
        Assert.IsTrue(runner.LoginStopped);
        Assert.IsFalse(deadline.IsCancellationRequested);
        Assert.AreEqual(AiLoginStage.Completed, progress.Values[^1].Stage);
        Assert.IsGreaterThanOrEqualTo(2, runner.Calls.Count(x => x.SequenceEqual(new[] { "login", "status" })));
        // Completion releases the exclusive lease only after login and its probe are cleaned up.
        using var lease = fixture.Paths.AcquireOperation();
    }

    [TestMethod]
    public async Task SuccessfulExitAndBannerWithoutSavedChatGptSessionDoNotCompleteLogin()
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var runner = new LoginRunner { SaveCredentials = false };
        var progress = new Updates();
        var error = await Assert.ThrowsExactlyAsync<ManagedAiException>(() => Auth(fixture, runner).LoginAsync(AiLoginMethod.Browser, progress, CancellationToken.None));
        Assert.AreEqual(AiFailureCode.AuthenticationRequired, error.Code);
        Assert.IsFalse(progress.Values.Any(x => x.Stage == AiLoginStage.Completed));
    }

    [TestMethod]
    public async Task CancellingPendingLoginStopsItWithoutReportingCompletion()
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var runner = new LoginRunner { KeepRunning = true, SaveCredentials = false };
        var progress = new Updates();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        await Assert.ThrowsAsync<OperationCanceledException>(() => Auth(fixture, runner).LoginAsync(AiLoginMethod.Browser, progress, cancellation.Token));
        Assert.IsTrue(runner.LoginStopped);
        Assert.IsFalse(progress.Values.Any(x => x.Stage == AiLoginStage.Completed));
        using var lease = fixture.Paths.AcquireOperation();
    }

    [TestMethod]
    public async Task DisabledDeviceLoginSurfacesBrowserFallbackFailureCode()
    {
        using var fixture = new InstallFixture();
        await fixture.Installer.InstallAsync(null, null, CancellationToken.None);
        var runner = new LoginRunner { Fail = true };
        var progress = new Updates();
        var error = await Assert.ThrowsExactlyAsync<ManagedAiException>(() => Auth(fixture, runner).LoginAsync(AiLoginMethod.DeviceCode, progress, CancellationToken.None));
        Assert.AreEqual(AiFailureCode.LoginFlowUnavailable, error.Code);
        Assert.IsFalse(progress.Values.Any(x => x.Stage == AiLoginStage.Completed));
    }

    private static OpenAiCodexAuthManager Auth(InstallFixture fixture, LoginRunner runner)
        => new(fixture.Paths, fixture.Installer, new WindowsManagedAiProfile(fixture.Paths), runner);
    private sealed class Updates : IProgress<AiLoginUpdate>
    {
        public List<AiLoginUpdate> Values = [];
        public void Report(AiLoginUpdate value) => Values.Add(value);
    }
    private sealed class LoginRunner : IJsonlProcessRunner
    {
        public bool LoggedIn, Fail;
        public bool SaveCredentials = true;
        public bool KeepRunning, LoginStopped;
        public List<string[]> Calls = [];
        public async Task<ProcessOutcome> RunAsync(ProcessRunSpec spec, Action<string> stdout, Action<string>? stderr, CancellationToken cancellationToken)
        {
            var args = spec.StartInfo.ArgumentList.ToArray();
            Calls.Add(args);
            if (args is ["login", "status"])
            {
                stderr?.Invoke(LoggedIn ? "Logged in using ChatGPT" : "Not logged in");
                return new ProcessOutcome(LoggedIn ? 0 : 1, 0, 32);
            }
            if (Fail) { stderr?.Invoke("Device code authentication is disabled for this account"); return new ProcessOutcome(1, 0, 64); }
            stdout(args.Contains("--device-auth") ? "https://auth.openai.com/codex/device" : "https://auth.openai.com/oauth/authorize?state=transient");
            stdout("ABCD-EFGHI");
            stdout("Successfully logged in");
            LoggedIn = SaveCredentials;
            try
            {
                if (KeepRunning) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return new ProcessOutcome(0, 100, 0);
            }
            finally { LoginStopped = true; }
        }
    }
}
