using System.Diagnostics;
using System.Text.Json;
using TableCloth.ManagedAi;

namespace TableCloth.Test;

[TestClass]
public sealed class ManagedAiSandboxBridgeTests
{
    private static readonly Guid SandboxId = Guid.Parse("12345678-1234-1234-1234-1234567890ab");

    [TestMethod]
    public void IntentRecognizesManagementRequestsWithoutRunningExplanatoryQuestions()
    {
        Assert.AreEqual(SandboxCliAction.List, WindowsSandboxIntent.Parse("Windows Sandbox 상태를 알려 주세요.")?.Action);
        Assert.AreEqual(SandboxCliAction.Start, WindowsSandboxIntent.Parse("새 샌드박스 시작해 주세요.")?.Action);
        Assert.AreEqual(SandboxCliAction.Start, WindowsSandboxIntent.Parse("샌드박스를 시작해 주세요.")?.Action);
        Assert.AreEqual(SandboxCliAction.List, WindowsSandboxIntent.Parse("wsb.exe list")?.Action);
        Assert.AreEqual(SandboxCliAction.Stop, WindowsSandboxIntent.Parse($"샌드박스 {SandboxId} 종료해 주세요.")?.Action);
        Assert.AreEqual(SandboxId, WindowsSandboxIntent.Parse($"샌드박스 {SandboxId} 종료해 주세요.")?.Id);
        Assert.IsNull(WindowsSandboxIntent.Parse("샌드박스 종료 방법을 알려 주세요."));
        Assert.IsNull(WindowsSandboxIntent.Parse("샌드박스에서 명령 실행해 주세요."));
        Assert.IsNull(WindowsSandboxIntent.Parse("샌드박스 폴더 공유해 주세요."));
        Assert.IsNull(WindowsSandboxIntent.Parse("은행 공식 사이트를 찾아 주세요."));
    }

    [TestMethod]
    public async Task BridgeSelectsOnlyRunningSessionAndPassesFixedCliArguments()
    {
        var runner = new Runner("{\"WindowsSandboxEnvironments\":[{\"Id\":\"" + SandboxId + "\"}]}", "{\"result\":\"stopped\"}");
        var bridge = new ManagedAiSandboxBridge(runner, @"C:\fixture\wsb.exe");
        var report = await bridge.ExecuteAsync(new(SandboxCliAction.Stop), CancellationToken.None);

        using var json = JsonDocument.Parse(report);
        Assert.AreEqual("succeeded", json.RootElement.GetProperty("Status").GetString());
        Assert.AreEqual(SandboxId.ToString("D"), json.RootElement.GetProperty("Id").GetString());
        CollectionAssert.AreEqual(new[] { "list", "--raw" }, runner.Commands[0]);
        CollectionAssert.AreEqual(new[] { "stop", "--id", SandboxId.ToString("D"), "--raw" }, runner.Commands[1]);
    }

    [TestMethod]
    public async Task BridgeDoesNotStopWhenSessionSelectionIsAmbiguous()
    {
        var runner = new Runner("{\"WindowsSandboxEnvironments\":[\"" + SandboxId + "\",\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"]}");
        var bridge = new ManagedAiSandboxBridge(runner, @"C:\fixture\wsb.exe");
        var report = await bridge.ExecuteAsync(new(SandboxCliAction.Stop), CancellationToken.None);

        using var json = JsonDocument.Parse(report);
        Assert.AreEqual("id-required", json.RootElement.GetProperty("Status").GetString());
        Assert.HasCount(1, runner.Commands);
    }

    [TestMethod]
    public async Task CliRunnerCapturesReadOnlyProcessOutput()
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? @"C:\Windows\System32\cmd.exe")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add("/c");
        start.ArgumentList.Add("echo sandbox-cli-runner");
        var result = await new SandboxCliProcessRunner().RunAsync(start, CancellationToken.None);
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual("sandbox-cli-runner", result.Stdout);
    }

    private sealed class Runner(params string[] output) : ISandboxCliProcessRunner
    {
        private int _index;
        public List<string[]> Commands { get; } = [];

        public Task<SandboxCliOutcome> RunAsync(ProcessStartInfo start, CancellationToken cancellationToken)
        {
            Assert.AreEqual(@"C:\fixture\wsb.exe", start.FileName);
            Assert.IsFalse(start.UseShellExecute);
            Commands.Add(start.ArgumentList.ToArray());
            return Task.FromResult(new SandboxCliOutcome(0, output[_index++], string.Empty));
        }
    }
}
