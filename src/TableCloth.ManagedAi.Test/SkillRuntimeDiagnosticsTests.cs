using Microsoft.VisualStudio.TestTools.UnitTesting;
using TableCloth.ManagedAi.OpenAi;

namespace TableCloth.ManagedAi.Test;

[TestClass]
public sealed class SkillRuntimeDiagnosticsTests
{
    [TestMethod]
    [DataRow(WindowsLongPathPolicy.Enabled)]
    [DataRow(WindowsLongPathPolicy.Disabled)]
    [DataRow(WindowsLongPathPolicy.Unknown)]
    [DataRow(WindowsLongPathPolicy.Unsupported)]
    public async Task PolicyIsReportedIndependentlyFromSuccessfulRuntimeProbes(WindowsLongPathPolicy policy)
    {
        using var fixture = new Fixture();
        fixture.Policy = policy;
        var result = await fixture.Diagnostics.CheckAsync(CancellationToken.None);
        Assert.AreEqual(policy, result.WindowsPolicy);
        Assert.AreEqual(LongPathSupport.Supported, result.ManagedIo);
        Assert.AreEqual(LongPathSupport.Supported, result.NodeJs);
        Assert.AreEqual(LongPathSupport.Supported, result.Python);
        Assert.HasCount(2, fixture.Runner.Invocations);
        fixture.AssertClean();
    }

    [TestMethod]
    public async Task MissingPairDoesNotRunHostRuntimesAndStillChecksManagedIo()
    {
        using var fixture = new Fixture();
        fixture.Runtimes.Active = null;
        var result = await fixture.Diagnostics.CheckAsync(CancellationToken.None);
        Assert.AreEqual(LongPathSupport.Supported, result.ManagedIo);
        Assert.AreEqual(LongPathSupport.NotInstalled, result.NodeJs);
        Assert.AreEqual(LongPathSupport.NotInstalled, result.Python);
        Assert.IsEmpty(fixture.Runner.Invocations);
        fixture.AssertClean();
    }

    [TestMethod]
    public async Task PolicyReadDeniedDoesNotOverrideActualSupport()
    {
        using var fixture = new Fixture();
        var diagnostics = new SkillRuntimeDiagnostics(fixture.Paths, fixture.Runtimes, fixture.Runner,
            () => throw new UnauthorizedAccessException());
        var result = await diagnostics.CheckAsync(CancellationToken.None);
        Assert.AreEqual(WindowsLongPathPolicy.Unknown, result.WindowsPolicy);
        Assert.AreEqual(LongPathSupport.Supported, result.Python);
        fixture.AssertClean();
    }

    [TestMethod]
    public async Task PythonLengthFailureIsLimitedWithoutChangingNodeSupport()
    {
        using var fixture = new Fixture();
        fixture.Runner.PythonOutput = "TABLECLOTH_LONGPATH_LIMITED";
        var result = await fixture.Diagnostics.CheckAsync(CancellationToken.None);
        Assert.AreEqual(LongPathSupport.Supported, result.NodeJs);
        Assert.AreEqual(LongPathSupport.Limited, result.Python);
        fixture.AssertClean();
    }

    [TestMethod]
    [DataRow("TABLECLOTH_LONGPATH_UNKNOWN", false, 0)]
    [DataRow("PermissionError: [WinError 5] Access is denied", false, 0)]
    [DataRow("TABLECLOTH_LONGPATH_SUPPORTED", true, 0)]
    [DataRow("TABLECLOTH_LONGPATH_LIMITED", false, 1)]
    [DataRow("TABLECLOTH_LONGPATH_SUPPORTED\nTABLECLOTH_LONGPATH_LIMITED", false, 0)]
    public async Task PermissionProtocolAndProcessFailuresRemainUnknown(string output, bool stderr, int exitCode)
    {
        using var fixture = new Fixture();
        fixture.Runner.PythonOutput = output;
        fixture.Runner.PythonStderr = stderr;
        fixture.Runner.PythonExitCode = exitCode;
        var result = await fixture.Diagnostics.CheckAsync(CancellationToken.None);
        Assert.AreEqual(LongPathSupport.Unknown, result.Python);
        Assert.AreEqual(LongPathSupport.Supported, result.NodeJs);
        fixture.AssertClean();
    }

    [TestMethod]
    public async Task UsesOnlyPrivateExecutablesAndIsolatedOwnedProcessEnvironment()
    {
        using var fixture = new Fixture();
        await fixture.Diagnostics.CheckAsync(CancellationToken.None);
        var node = fixture.Runner.Invocations[0];
        var python = fixture.Runner.Invocations[1];
        Assert.AreEqual(fixture.Runtimes.Active!.NodeJs.EntryPoint, node.StartInfo.FileName);
        Assert.AreEqual(fixture.Runtimes.Active.Python.EntryPoint, python.StartInfo.FileName);
        CollectionAssert.AreEqual(new[] { "-I", "-S", "-c" }, python.StartInfo.ArgumentList.Take(3).ToArray());
        Assert.AreEqual("-e", node.StartInfo.ArgumentList[0]);
        foreach (var spec in fixture.Runner.Invocations)
        {
            Assert.IsFalse(spec.StartInfo.UseShellExecute);
            Assert.IsTrue(spec.StartInfo.CreateNoWindow);
            Assert.IsNull(spec.Input);
            Assert.IsNull(spec.Respond);
            Assert.AreEqual(TimeSpan.FromSeconds(20), spec.Timeout);
            Assert.IsLessThanOrEqualTo(4096, spec.MaxStdoutBytes);
            Assert.IsLessThanOrEqualTo(4096, spec.MaxStderrBytes);
            CollectionAssert.AreEquivalent(new[] { "SystemRoot", "WINDIR", "TEMP", "TMP", "HOME", "USERPROFILE" },
                spec.StartInfo.Environment.Keys.ToArray());
            Assert.IsFalse(spec.StartInfo.Environment.ContainsKey("PATH"));
            Assert.IsFalse(spec.StartInfo.Environment.ContainsKey("NODE_OPTIONS"));
            Assert.IsFalse(spec.StartInfo.Environment.ContainsKey("PYTHONPATH"));
            var arguments = spec.StartInfo.ArgumentList;
            var shortPath = arguments[^2];
            var longPath = arguments[^1];
            Assert.IsLessThan(260, Path.Combine(shortPath, "renamed.txt").Length);
            Assert.IsGreaterThanOrEqualTo(340, Path.Combine(longPath, "renamed.txt").Length);
            Assert.IsTrue(shortPath.StartsWith(spec.StartInfo.WorkingDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal));
            Assert.IsTrue(longPath.StartsWith(spec.StartInfo.WorkingDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal));
            foreach (var name in new[] { "TEMP", "TMP", "HOME", "USERPROFILE" })
                Assert.IsTrue(spec.StartInfo.Environment[name]!.StartsWith(spec.StartInfo.WorkingDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        }
        fixture.AssertClean();
    }

    [TestMethod]
    [DataRow("timeout")]
    [DataRow("permission")]
    [DataRow("startup-length")]
    [DataRow("internal-cancellation")]
    public async Task ProcessStartupAndTimeoutErrorsDoNotClaimALongPathLimit(string failure)
    {
        using var fixture = new Fixture();
        fixture.Runner.PythonFailure = failure switch
        {
            "timeout" => new ManagedAiException(AiFailureCode.ProviderTimeout),
            "permission" => new UnauthorizedAccessException(),
            "startup-length" => new PathTooLongException(),
            _ => new OperationCanceledException()
        };
        var result = await fixture.Diagnostics.CheckAsync(CancellationToken.None);
        Assert.AreEqual(LongPathSupport.Unknown, result.Python);
        Assert.AreEqual(LongPathSupport.Supported, result.NodeJs);
        fixture.AssertClean();
    }

    [TestMethod]
    public async Task RuntimeOutsideOwnedReleasesIsNotExecuted()
    {
        using var fixture = new Fixture();
        fixture.Runtimes.Active = fixture.Runtimes.Active! with { Python = new("3.14.9", Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "python.exe")) };
        var result = await fixture.Diagnostics.CheckAsync(CancellationToken.None);
        Assert.AreEqual(LongPathSupport.Unknown, result.Python);
        Assert.AreEqual(LongPathSupport.Supported, result.NodeJs);
        Assert.HasCount(1, fixture.Runner.Invocations);
        fixture.AssertClean();
    }

    [TestMethod]
    public async Task RuntimeInventoryFailureIsUnknownRatherThanNotInstalled()
    {
        using var fixture = new Fixture();
        fixture.Runtimes.Failure = new ManagedAiException(AiFailureCode.IntegrityMismatch);
        var result = await fixture.Diagnostics.CheckAsync(CancellationToken.None);
        Assert.AreEqual(LongPathSupport.Unknown, result.NodeJs);
        Assert.AreEqual(LongPathSupport.Unknown, result.Python);
        Assert.AreEqual(LongPathSupport.Supported, result.ManagedIo);
        Assert.IsEmpty(fixture.Runner.Invocations);
        fixture.AssertClean();
    }

    [TestMethod]
    public async Task CancellationWaitsForProcessCleanupBeforeRemovingOwnedProbe()
    {
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string? probe = null;
        fixture.Runner.BeforeReturn = async (spec, token) =>
        {
            probe = spec.StartInfo.WorkingDirectory;
            File.WriteAllText(Path.Combine(probe, "child-owned.txt"), "probe");
            started.SetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { await cleanup.Task; }
        };
        var operation = fixture.Diagnostics.CheckAsync(cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        Assert.IsFalse(operation.IsCompleted);
        Assert.IsTrue(Directory.Exists(probe));
        cleanup.SetResult();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await operation);
        Assert.IsFalse(Directory.Exists(probe));
        fixture.AssertClean();
    }

    [TestMethod]
    public async Task DiagnosticsCanRunWhileMutationLeaseIsHeld()
    {
        using var fixture = new Fixture();
        using var lease = fixture.Paths.AcquireOperation();
        var result = await fixture.Diagnostics.CheckAsync(CancellationToken.None);
        Assert.AreEqual(LongPathSupport.Supported, result.NodeJs);
        Assert.AreEqual(LongPathSupport.Supported, result.Python);
        fixture.AssertClean();
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Paths = new(Path.Combine(Path.GetTempPath(), "tc-diagnostics-" + Guid.NewGuid().ToString("N")));
            var release = Paths.Under("runtimes", "skill-tools", "releases", Guid.NewGuid().ToString("N"));
            var node = Path.Combine(release, "node");
            var python = Path.Combine(release, "python");
            Directory.CreateDirectory(node);
            Directory.CreateDirectory(python);
            File.WriteAllText(Path.Combine(node, "node.exe"), "private node fixture");
            File.WriteAllText(Path.Combine(python, "python.exe"), "private python fixture");
            Runtimes.Active = new(new("24.22.1", node, Path.Combine(node, "node.exe")),
                new("3.14.9", python, Path.Combine(python, "python.exe")));
        }
        public ManagedAiPaths Paths { get; }
        public RuntimeManager Runtimes { get; } = new();
        public ProbeRunner Runner { get; } = new();
        public WindowsLongPathPolicy Policy = WindowsLongPathPolicy.Enabled;
        public SkillRuntimeDiagnostics Diagnostics => new(Paths, Runtimes, Runner, () => Policy);
        public void AssertClean()
        {
            var directory = Paths.Under("profiles", "skill-tools", "diagnostics");
            if (Directory.Exists(directory)) Assert.IsEmpty(Directory.EnumerateFileSystemEntries(directory));
            if (Runtimes.Active is { } active && active.NodeJs.RootDirectory.StartsWith(Paths.Root, StringComparison.Ordinal))
                Assert.IsTrue(File.Exists(active.NodeJs.EntryPoint));
        }
        public void Dispose() => Directory.Delete(Paths.Root, recursive: true);
    }

    private sealed class RuntimeManager : IManagedAiSkillRuntimeManager
    {
        public SkillRuntimeEnvironment? Active;
        public Exception? Failure;
        public Task<SkillRuntimeEnvironment?> GetActiveAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (Failure is not null) throw Failure;
            return Task.FromResult(Active);
        }
        public Task<SkillRuntimeEnvironment> InstallAsync(IProgress<AiProgress>? progress, CancellationToken token)
            => throw new AssertFailedException("Diagnostics must not install or mutate runtimes.");
    }

    private sealed class ProbeRunner : IJsonlProcessRunner
    {
        public string PythonOutput = "TABLECLOTH_LONGPATH_SUPPORTED";
        public bool PythonStderr;
        public int PythonExitCode;
        public Exception? PythonFailure;
        public Func<ProcessRunSpec, CancellationToken, Task>? BeforeReturn;
        public List<ProcessRunSpec> Invocations { get; } = [];
        public async Task<ProcessOutcome> RunAsync(ProcessRunSpec spec, Action<string> stdout, Action<string>? stderr, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Invocations.Add(spec);
            if (BeforeReturn is not null) await BeforeReturn(spec, token);
            var python = Path.GetFileName(spec.StartInfo.FileName) == "python.exe";
            if (python && PythonFailure is not null) throw PythonFailure;
            var output = python ? PythonOutput : "TABLECLOTH_LONGPATH_SUPPORTED";
            foreach (var line in output.Split('\n')) stdout(line);
            if (python && PythonStderr) stderr?.Invoke("PermissionError");
            return new(python ? PythonExitCode : 0, output.Length, 0);
        }
    }
}
