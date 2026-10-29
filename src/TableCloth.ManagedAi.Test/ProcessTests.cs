using System.Diagnostics;
using System.Text;
using TableCloth.ManagedAi.Windows;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class ProcessTests
{
    private static ProcessRunSpec Spec(string script, TimeSpan? timeout = null) => new(new ProcessStartInfo
    {
        FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
        UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
        RedirectStandardOutput = true, RedirectStandardError = true,
        ArgumentList = { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand",
            Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) }
    }, null, timeout ?? TimeSpan.FromSeconds(20), 1024 * 1024, 1024 * 1024, 65536);

    [TestMethod]
    public async Task HandlesSplitUtf8LinesAndSimultaneousStderr()
    {
        var spec = Spec("""
            $bytes = [Text.Encoding]::UTF8.GetBytes("한글`nlast")
            $stream = [Console]::OpenStandardOutput()
            foreach ($b in $bytes) { $stream.WriteByte($b); $stream.Flush(); Start-Sleep -Milliseconds 5 }
            for ($i=0; $i -lt 2000; $i++) { [Console]::Error.WriteLine('diagnostic') }
            """);
        var lines = new List<string>();
        var result = await new JsonlProcessRunner().RunAsync(spec, lines.Add, null, CancellationToken.None);
        CollectionAssert.AreEqual(new[] { "한글", "last" }, lines);
        Assert.AreEqual(0, result.ExitCode);
        Assert.IsGreaterThan(20000L, result.StderrBytes);
    }

    [TestMethod]
    public async Task OverlongLineKillsProcessAndNextRunSucceeds()
    {
        var runner = new JsonlProcessRunner();
        var ex = await Assert.ThrowsExactlyAsync<ManagedAiException>(() => runner.RunAsync(
            Spec("[Console]::Write(('x' * 10000)); Start-Sleep -Seconds 30") with { MaxLineBytes = 1024 }, _ => { }, null, CancellationToken.None));
        Assert.AreEqual(AiFailureCode.OutputLimitExceeded, ex.Code);
        var next = await runner.RunAsync(Spec("[Console]::WriteLine('ok')"), _ => { }, null, CancellationToken.None);
        Assert.AreEqual(0, next.ExitCode);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CancellationAndTimeoutTerminateDescendants(bool cancel)
    {
        var spec = Spec("""
            $p = New-Object System.Diagnostics.ProcessStartInfo
            $p.FileName = (Get-Process -Id $PID).Path
            $p.Arguments = '-NoProfile -NonInteractive -Command "Start-Sleep -Seconds 60"'
            $p.UseShellExecute = $false
            $p.CreateNoWindow = $true
            $child = [Diagnostics.Process]::Start($p)
            [Console]::WriteLine($child.Id)
            Start-Sleep -Seconds 60
            """, cancel ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(3));
        using var cancellation = new CancellationTokenSource();
        var childId = 0;
        var run = new JsonlProcessRunner().RunAsync(spec, line =>
        {
            if (int.TryParse(line, out var id))
            {
                childId = id;
                if (cancel) cancellation.CancelAfter(100);
            }
        }, null, cancellation.Token);
        if (cancel) await Assert.ThrowsAsync<OperationCanceledException>(() => run);
        else Assert.AreEqual(AiFailureCode.ProviderTimeout, (await Assert.ThrowsExactlyAsync<ManagedAiException>(() => run)).Code);
        Assert.IsGreaterThan(0, childId);
        try
        {
            using var child = Process.GetProcessById(childId);
            Assert.IsTrue(child.WaitForExit(5000));
        }
        catch (ArgumentException) { /* Already reaped. */ }
    }

    [TestMethod]
    public async Task ParentExitTerminatesChildHoldingOutputPipesWithoutWaitingForTimeout()
    {
        var spec = Spec("""
            $p = New-Object System.Diagnostics.ProcessStartInfo
            $p.FileName = (Get-Process -Id $PID).Path
            $p.Arguments = '-NoProfile -NonInteractive -Command "Start-Sleep -Seconds 60"'
            $p.UseShellExecute = $false
            $p.CreateNoWindow = $true
            $child = [Diagnostics.Process]::Start($p)
            [Console]::WriteLine($child.Id)
            exit 0
            """, TimeSpan.FromSeconds(4));
        var childId = 0;
        var result = await new JsonlProcessRunner().RunAsync(spec, line =>
        {
            if (int.TryParse(line, out var id)) childId = id;
        }, null, CancellationToken.None);
        Assert.AreEqual(0, result.ExitCode);
        Assert.IsGreaterThan(0, childId);
        try
        {
            using var child = Process.GetProcessById(childId);
            Assert.IsTrue(child.WaitForExit(3000));
        }
        catch (ArgumentException) { /* Already reaped. */ }
    }

    [TestMethod]
    public async Task DuplexExchangeFlushesRepliesAndClosesOnlyAfterFinalResponse()
    {
        var spec = Spec("""
            $first = [Console]::ReadLine()
            [Console]::WriteLine('ready')
            $second = [Console]::ReadLine()
            [Console]::WriteLine("$first/$second")
            $eof = [Console]::ReadLine()
            if ($null -ne $eof) { exit 1 }
            """) with
        {
            Input = "hello\n",
            Respond = line => line == "ready" ? new("reply\n") : new(Close: true)
        };
        var lines = new List<string>();
        var outcome = await new JsonlProcessRunner().RunAsync(spec, lines.Add, null, CancellationToken.None);
        Assert.AreEqual(0, outcome.ExitCode);
        CollectionAssert.AreEqual(new[] { "ready", "hello/reply" }, lines);
    }

    [TestMethod]
    public async Task StalledDuplexExchangeHonorsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var spec = Spec("[Console]::WriteLine('ready'); $null = [Console]::ReadLine()") with { Respond = _ => null };
        await Assert.ThrowsAsync<OperationCanceledException>(() => new JsonlProcessRunner().RunAsync(spec,
            _ => cancellation.Cancel(), null, cancellation.Token));
    }

    [TestMethod]
    public async Task AlreadyCancelledRunNeverStarts()
    {
        using var token = new CancellationTokenSource();
        token.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => new JsonlProcessRunner().RunAsync(
            Spec("Start-Sleep -Seconds 30"), _ => Assert.Fail(), null, token.Token));
    }
}
