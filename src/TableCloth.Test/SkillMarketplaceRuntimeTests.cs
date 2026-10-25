using Avalonia.Controls;
using Avalonia.Threading;
using TableCloth.Dialogs;
using TableCloth.ManagedAi;
using TableCloth.ViewModels;

namespace TableCloth.Test;

public sealed partial class SkillMarketplaceTests
{
    [TestMethod]
    public Task RuntimeInstallWorksAfterCatalogFailureAndSharesTheLocalBusyGate() => OnUiThread(async () =>
    {
        using var culture = new UiCultureScope("en-US");
        var manager = new SkillManager();
        var runtime = new SkillRuntimeManager
        {
            Active = RuntimeEnvironment("22.0.0", "3.12.0"),
            InstallGate = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        var diagnostics = new SkillRuntimeDiagnostics();
        using var model = new SkillMarketplaceWindowViewModel(manager, new Marketplace(manager) { Failure = true }, new Messages(), runtime, diagnostics);
        await model.LoadAsync();
        model.SelectedInstalledSkill = model.InstalledSkills.Single();
        Assert.Contains("Could not load the remote catalog", model.CatalogNotice);
        Assert.AreEqual("22.0.0", model.NodeJsRuntimeVersion);
        Assert.AreEqual("3.12.0", model.PythonRuntimeVersion);
        Assert.AreEqual(1, diagnostics.Checks);
        Assert.IsFalse(model.HasSkillRuntimeUnknown);
        Assert.IsFalse(runtime.StatusCalledOnUiThread ?? true);
        var install = model.InstallSkillRuntimesCommand.ExecuteAsync(null);
        await runtime.InstallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(model.LocalBusy);
        Assert.IsTrue(model.SkillRuntimeBusy);
        Assert.IsFalse(model.ToggleSkillCommand.CanExecute(null));
        Assert.IsFalse(model.RemoveSkillCommand.CanExecute(null));
        Assert.IsFalse(model.ImportSkillCommand.CanExecute(null));
        Assert.IsFalse(model.RefreshSkillRuntimeStatusCommand.CanExecute(null));
        Assert.IsFalse(model.InstallSkillRuntimesCommand.CanExecute(null));
        runtime.InstallGate.SetResult();
        await install;
        Assert.IsFalse(runtime.InstallCalledOnUiThread ?? true);
        Assert.AreEqual("24.1.0", model.NodeJsRuntimeVersion);
        Assert.AreEqual("3.13.1", model.PythonRuntimeVersion);
        Assert.AreEqual("Dedicated Node.js and Python installed.", model.SkillRuntimeNotice);
        Assert.AreEqual(2, diagnostics.Checks);
        Assert.IsTrue(model.ToggleSkillCommand.CanExecute(null));
        Assert.IsTrue(model.RemoveSkillCommand.CanExecute(null));
        Assert.IsTrue(model.InstallSkillRuntimesCommand.CanExecute(null));
    });

    [TestMethod]
    public Task RuntimeStatusQueryKeepsLocalSkillControlsAvailable() => OnUiThread(async () =>
    {
        var manager = new SkillManager();
        var runtime = new SkillRuntimeManager { StatusGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var model = new SkillMarketplaceWindowViewModel(manager, new Marketplace(manager), new Messages(), runtime);
        var load = model.LoadAsync();
        await runtime.StatusStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Until(() => !model.LocalBusy);
        model.SelectedInstalledSkill = model.InstalledSkills.Single();
        Assert.IsTrue(model.SkillRuntimeStatusBusy);
        Assert.IsTrue(model.ToggleSkillCommand.CanExecute(null));
        Assert.IsTrue(model.RemoveSkillCommand.CanExecute(null));
        Assert.IsFalse(model.InstallSkillRuntimesCommand.CanExecute(null));
        runtime.StatusGate.SetResult();
        await load;
        Assert.IsFalse(model.SkillRuntimeBusy);
        Assert.AreEqual(ManagedAiText.Select("설치 안 함", "Not installed"), model.NodeJsRuntimeVersion);
        Assert.AreEqual(ManagedAiText.Select("설치 안 함", "Not installed"), model.PythonRuntimeVersion);
        Assert.IsTrue(model.InstallSkillRuntimesCommand.CanExecute(null));
    });

    [TestMethod]
    public Task RuntimeInstallFailureAndCancellationKeepThePreviousVersionAndLocalControls() => OnUiThread(async () =>
    {
        using var culture = new UiCultureScope("en-US");
        var manager = new SkillManager();
        var runtime = new SkillRuntimeManager { Active = RuntimeEnvironment("22.0.0", "3.12.0"), FailureCode = AiFailureCode.OfficialMetadataUnavailable };
        using var model = new SkillMarketplaceWindowViewModel(manager, new Marketplace(manager), new Messages(), runtime);
        await model.LoadAsync();
        model.SelectedInstalledSkill = model.InstalledSkills.Single();
        await model.InstallSkillRuntimesCommand.ExecuteAsync(null);
        Assert.Contains("Could not download", model.SkillRuntimeNotice);
        Assert.AreEqual("22.0.0", model.NodeJsRuntimeVersion);
        Assert.AreEqual("3.12.0", model.PythonRuntimeVersion);
        Assert.IsTrue(model.RemoveSkillCommand.CanExecute(null));
        runtime.FailureCode = null;
        runtime.CancelInstall = true;
        await model.InstallSkillRuntimesCommand.ExecuteAsync(null);
        Assert.AreEqual("Runtime installation canceled.", model.SkillRuntimeNotice);
        Assert.AreEqual("22.0.0", model.NodeJsRuntimeVersion);
        Assert.IsTrue(model.RemoveSkillCommand.CanExecute(null));
        Assert.IsFalse(model.LocalBusy);
    });

    [TestMethod]
    public Task ClosingMarketplaceCancelsRuntimeInstallationAndWaitsForCleanup() => OnUiThread(async () =>
    {
        var manager = new SkillManager();
        var runtime = new SkillRuntimeManager
        {
            Active = RuntimeEnvironment("22.0.0", "3.12.0"),
            InstallGate = new(TaskCreationOptions.RunContinuationsAsynchronously),
            CleanupGate = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        using var model = new SkillMarketplaceWindowViewModel(manager, new Marketplace(manager), new Messages(), runtime);
        var window = new SkillMarketplaceWindow(model, SkillMarketplaceTab.Installed);
        try
        {
            window.Show();
            await runtime.StatusStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Until(() => !model.LocalBusy && !model.SkillRuntimeBusy && !model.CatalogBusy);
            var install = model.InstallSkillRuntimesCommand.ExecuteAsync(null);
            await runtime.InstallStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            window.Close();
            await runtime.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var cleanup = model.WaitForLocalOperationAsync();
            Assert.IsFalse(cleanup.IsCompleted);
            runtime.CleanupGate.SetResult();
            await Task.WhenAll(install, cleanup);
            Assert.IsFalse(model.LocalBusy);
            Assert.AreEqual("22.0.0", model.NodeJsRuntimeVersion);
            Assert.IsFalse(model.InstallSkillRuntimesCommand.CanExecute(null));
        }
        finally { runtime.CleanupGate.TrySetResult(); window.Close(); }
    });

    private static SkillRuntimeEnvironment RuntimeEnvironment(string nodeVersion, string pythonVersion) => new(
        new(nodeVersion, "C:\\fixture-runtimes\\node", "C:\\fixture-runtimes\\node\\node.exe"),
        new(pythonVersion, "C:\\fixture-runtimes\\python", "C:\\fixture-runtimes\\python\\python.exe"));

    private sealed class SkillRuntimeManager : IManagedAiSkillRuntimeManager
    {
        public SkillRuntimeEnvironment? Active;
        public SkillRuntimeEnvironment Next = RuntimeEnvironment("24.1.0", "3.13.1");
        public bool? StatusCalledOnUiThread;
        public bool? InstallCalledOnUiThread;
        public AiFailureCode? FailureCode;
        public bool CancelInstall;
        public TaskCompletionSource? StatusGate;
        public TaskCompletionSource? InstallGate;
        public TaskCompletionSource? CleanupGate;
        public TaskCompletionSource StatusStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource InstallStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<SkillRuntimeEnvironment?> GetActiveAsync(CancellationToken cancellationToken)
        {
            StatusCalledOnUiThread = Dispatcher.UIThread.CheckAccess();
            StatusStarted.TrySetResult();
            if (StatusGate is not null) await StatusGate.Task.WaitAsync(cancellationToken);
            return Active;
        }

        public async Task<SkillRuntimeEnvironment> InstallAsync(IProgress<AiProgress>? progress, CancellationToken cancellationToken)
        {
            InstallCalledOnUiThread = Dispatcher.UIThread.CheckAccess();
            InstallStarted.TrySetResult();
            progress?.Report(new("SkillRuntimeDownloadingNode"));
            if (FailureCode is { } code) throw new ManagedAiException(code);
            if (CancelInstall) throw new OperationCanceledException();
            try { if (InstallGate is not null) await InstallGate.Task.WaitAsync(cancellationToken); }
            catch (OperationCanceledException)
            {
                CancellationObserved.TrySetResult();
                if (CleanupGate is not null) await CleanupGate.Task;
                throw;
            }
            Active = Next;
            return Next;
        }
    }
}
