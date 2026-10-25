using Avalonia.Threading;
using TableCloth.Dialogs;
using TableCloth.ManagedAi;
using TableCloth.ViewModels;

namespace TableCloth.Test;

public sealed partial class SkillMarketplaceTests
{
    [TestMethod]
    public Task OptionsFactoryPassesDiagnosticsAndPolicyOffDoesNotBlockRuntimeInstallation() => OnUiThread(async () =>
    {
        using var culture = new UiCultureScope("en-US");
        var manager = new SkillManager();
        var runtime = new SkillRuntimeManager { Active = RuntimeEnvironment("22.0.0", "3.12.0") };
        var diagnostics = new SkillRuntimeDiagnostics
        {
            Result = new(WindowsLongPathPolicy.Disabled, LongPathSupport.Supported, LongPathSupport.Supported, LongPathSupport.Supported)
        };
        var options = new OptionsWindowViewModel(null!, null!, new Messages(), null!, new TaskFactory(), manager, new Marketplace(manager), runtime, diagnostics);
        using var model = options.CreateSkillMarketplaceViewModel();
        Assert.IsNotNull(model);
        await model.LoadAsync();
        Assert.AreEqual(1, diagnostics.Checks);
        Assert.IsFalse(diagnostics.CalledOnUiThread ?? true);
        Assert.AreEqual("Disabled", model.WindowsLongPathStatus);
        Assert.AreEqual("Long-path check passed", model.ManagedLongPathStatus);
        Assert.AreEqual("Long-path check passed", model.NodeJsLongPathStatus);
        Assert.AreEqual("Long-path check passed", model.PythonLongPathStatus);
        Assert.IsTrue(model.HasSkillRuntimeLimits);
        Assert.IsFalse(model.HasSkillRuntimeUnknown);
        Assert.IsTrue(model.InstallSkillRuntimesCommand.CanExecute(null));
        await model.RefreshSkillRuntimeStatusCommand.ExecuteAsync(null);
        Assert.AreEqual(2, diagnostics.Checks);
        diagnostics.Result = SupportedDiagnostics();
        await model.InstallSkillRuntimesCommand.ExecuteAsync(null);
        Assert.AreEqual(3, diagnostics.Checks);
        Assert.AreEqual("24.1.0", model.NodeJsRuntimeVersion);
        Assert.IsFalse(model.HasSkillRuntimeLimits);
        Assert.IsFalse(model.HasSkillRuntimeUnknown);
    });

    [TestMethod]
    public Task LongPathLimitsAndUnknownResultsHaveSeparateNonblockingStates() => OnUiThread(async () =>
    {
        using var culture = new UiCultureScope("en-US");
        var manager = new SkillManager();
        var runtime = new SkillRuntimeManager { Active = RuntimeEnvironment("22.0.0", "3.12.0") };
        var diagnostics = new SkillRuntimeDiagnostics
        {
            Result = new(WindowsLongPathPolicy.Enabled, LongPathSupport.Limited, LongPathSupport.Supported, LongPathSupport.NotInstalled)
        };
        using var model = new SkillMarketplaceWindowViewModel(manager, new Marketplace(manager), new Messages(), runtime, diagnostics);
        await model.LoadAsync();
        Assert.AreEqual("Long-path limited", model.ManagedLongPathStatus);
        Assert.AreEqual("Not installed", model.PythonLongPathStatus);
        Assert.IsTrue(model.HasSkillRuntimeLimits);
        Assert.IsFalse(model.HasSkillRuntimeUnknown);
        Assert.IsTrue(model.InstallSkillRuntimesCommand.CanExecute(null));
        diagnostics.Result = new(WindowsLongPathPolicy.Unknown, LongPathSupport.Unknown, LongPathSupport.Unknown, LongPathSupport.Unknown);
        await model.RefreshSkillRuntimeStatusCommand.ExecuteAsync(null);
        Assert.AreEqual("Unknown", model.WindowsLongPathStatus);
        Assert.AreEqual("Unknown", model.ManagedLongPathStatus);
        Assert.IsFalse(model.HasSkillRuntimeLimits);
        Assert.IsTrue(model.HasSkillRuntimeUnknown);
        Assert.IsTrue(model.InstallSkillRuntimesCommand.CanExecute(null));
        diagnostics.Failure = true;
        await model.RefreshSkillRuntimeStatusCommand.ExecuteAsync(null);
        Assert.AreEqual("22.0.0", model.NodeJsRuntimeVersion);
        Assert.AreEqual("Unknown", model.NodeJsLongPathStatus);
        Assert.IsTrue(model.HasSkillRuntimeUnknown);
    });

    [TestMethod]
    public Task DiagnosticsRemainIndependentOfLocalControlsAndCloseWaitsForProbeCleanup() => OnUiThread(async () =>
    {
        var manager = new SkillManager();
        var runtime = new SkillRuntimeManager { Active = RuntimeEnvironment("22.0.0", "3.12.0") };
        var diagnostics = new SkillRuntimeDiagnostics
        {
            Gate = new(TaskCreationOptions.RunContinuationsAsynchronously),
            CleanupGate = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        using var model = new SkillMarketplaceWindowViewModel(manager, new Marketplace(manager), new Messages(), runtime, diagnostics);
        var window = new SkillMarketplaceWindow(model);
        try
        {
            window.Show();
            await diagnostics.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Until(() => !model.LocalBusy);
            model.SelectedInstalledSkill = model.InstalledSkills.Single();
            Assert.IsTrue(model.SkillRuntimeStatusBusy);
            Assert.IsTrue(model.RemoveSkillCommand.CanExecute(null));
            Assert.IsTrue(model.ToggleSkillCommand.CanExecute(null));
            window.Close();
            await diagnostics.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var cleanup = model.WaitForLocalOperationAsync();
            Assert.IsFalse(cleanup.IsCompleted);
            diagnostics.CleanupGate.SetResult();
            await cleanup;
            Assert.IsFalse(model.SkillRuntimeStatusBusy);
        }
        finally { diagnostics.CleanupGate.TrySetResult(); window.Close(); }
    });

    [TestMethod]
    public Task CloseDuringPostInstallDiagnosticsWaitsForCleanupAndKeepsTheActivatedVersion() => OnUiThread(async () =>
    {
        var manager = new SkillManager();
        var runtime = new SkillRuntimeManager { Active = RuntimeEnvironment("22.0.0", "3.12.0") };
        var diagnostics = new SkillRuntimeDiagnostics();
        using var model = new SkillMarketplaceWindowViewModel(manager, new Marketplace(manager), new Messages(), runtime, diagnostics);
        var window = new SkillMarketplaceWindow(model);
        try
        {
            window.Show();
            await Until(() => diagnostics.Checks == 1 && !model.LocalBusy && !model.SkillRuntimeBusy && !model.CatalogBusy);
            diagnostics.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            diagnostics.CleanupGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var install = model.InstallSkillRuntimesCommand.ExecuteAsync(null);
            await Until(() => diagnostics.Checks == 2 && model.SkillRuntimeInstalling);
            window.Close();
            await diagnostics.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var cleanup = model.WaitForLocalOperationAsync();
            Assert.IsFalse(cleanup.IsCompleted);
            diagnostics.CleanupGate.SetResult();
            await Task.WhenAll(install, cleanup);
            Assert.AreEqual("24.1.0", model.NodeJsRuntimeVersion);
            Assert.IsFalse(model.LocalBusy);
        }
        finally { diagnostics.CleanupGate?.TrySetResult(); window.Close(); }
    });

    [TestMethod]
    public Task PathLengthInstallationFailureHasADedicatedMessage() => OnUiThread(async () =>
    {
        using var culture = new UiCultureScope("en-US");
        var manager = new SkillManager();
        var runtime = new SkillRuntimeManager { Active = RuntimeEnvironment("22.0.0", "3.12.0"), FailureCode = AiFailureCode.PathLengthUnsupported };
        using var model = new SkillMarketplaceWindowViewModel(manager, new Marketplace(manager), new Messages(), runtime, new SkillRuntimeDiagnostics());
        await model.LoadAsync();
        await model.InstallSkillRuntimesCommand.ExecuteAsync(null);
        Assert.Contains("path-length limit", model.SkillRuntimeNotice);
        Assert.AreEqual("22.0.0", model.NodeJsRuntimeVersion);
        Assert.IsFalse(model.LocalBusy);
    });

    private static SkillRuntimeDiagnosticsResult SupportedDiagnostics() =>
        new(WindowsLongPathPolicy.Enabled, LongPathSupport.Supported, LongPathSupport.Supported, LongPathSupport.Supported);

    private sealed class SkillRuntimeDiagnostics : IManagedAiSkillRuntimeDiagnostics
    {
        public SkillRuntimeDiagnosticsResult Result = SupportedDiagnostics();
        public bool? CalledOnUiThread;
        public int Checks;
        public bool Failure;
        public TaskCompletionSource? Gate;
        public TaskCompletionSource? CleanupGate;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<SkillRuntimeDiagnosticsResult> CheckAsync(CancellationToken cancellationToken)
        {
            CalledOnUiThread = Dispatcher.UIThread.CheckAccess();
            Interlocked.Increment(ref Checks);
            Started.TrySetResult();
            try { if (Gate is not null) await Gate.Task.WaitAsync(cancellationToken); }
            catch (OperationCanceledException)
            {
                CancellationObserved.TrySetResult();
                if (CleanupGate is not null) await CleanupGate.Task;
                throw;
            }
            if (Failure) throw new InvalidOperationException("Fixture diagnostic failure");
            return Result;
        }
    }
}
