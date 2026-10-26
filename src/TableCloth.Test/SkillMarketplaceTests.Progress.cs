using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TableCloth.Dialogs;
using TableCloth.ManagedAi;
using TableCloth.Theme.Controls;
using TableCloth.ViewModels;

namespace TableCloth.Test;

public sealed partial class SkillMarketplaceTests
{
    [TestMethod]
    [DataRow("en-US", false, true)]
    [DataRow("ko-KR", true, false)]
    public Task PendingDiscoveryShowsBoundProgressAndSuppressesPrematureEmptyResults(
        string cultureName, bool dark, bool catalogFirst) => OnUiThread(async () =>
    {
        using var culture = new UiCultureScope(cultureName);
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var manager = new SkillManager { Items = [], ListGate = NewProgressCompletion() };
        var remote = new Marketplace(manager) { Entries = [], ListGate = NewProgressCompletion() };
        using var model = new SkillMarketplaceWindowViewModel(manager, remote, new Messages());
        var window = ProgressWindow(model);
        var load = model.LoadAsync();
        try
        {
            await Task.WhenAll(manager.ListStarted.Task, remote.Started.Task).WaitAsync(TimeSpan.FromSeconds(5));
            Dispatcher.UIThread.RunJobs();
            Assert.IsTrue(model.CatalogBusy);
            Assert.IsTrue(model.LocalBusy);
            Assert.IsFalse(model.CatalogEmpty);
            Assert.IsFalse(model.InstalledEmpty);
            AssertProgressVisible(window, model);
            Assert.AreEqual(model.CatalogActivityText, window.FindControl<TextBlock>("CatalogActivity")!.Text);
            Assert.AreEqual(model.LocalActivityText, window.FindControl<TextBlock>("LocalActivity")!.Text);
            Assert.IsTrue(window.FindControl<TextBlock>("CatalogActivity")!.IsEffectivelyVisible);
            Assert.IsTrue(window.FindControl<TextBlock>("LocalActivity")!.IsEffectivelyVisible);
            Assert.IsFalse(window.GetVisualDescendants().OfType<TextBlock>()
                .Any(x => x.Text == ManagedAiText.MarketplaceNoResults && x.IsEffectivelyVisible));
            Assert.IsFalse(window.FindControl<Button>("CatalogRefresh")!.IsEffectivelyEnabled);
            await SaveProgressAsync(window, $"marketplace-progress-{cultureName}-{(dark ? "dark" : "light")}-loading.png");

            if (catalogFirst)
            {
                remote.ListGate.SetResult();
                await Until(() => !model.CatalogBusy);
                Assert.IsTrue(model.LocalBusy);
                Assert.IsFalse(model.InstalledEmpty);
            }
            else
            {
                manager.ListGate.SetResult();
                await Until(() => !model.LocalBusy);
                Assert.IsTrue(model.CatalogBusy);
                Assert.IsFalse(model.CatalogEmpty);
            }
            AssertProgressVisible(window, model);
            manager.ListGate.TrySetResult();
            remote.ListGate.TrySetResult();
            await load.WaitAsync(TimeSpan.FromSeconds(5));
            Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(model.IsBusy);
            Assert.IsFalse(model.HasLocalActivity);
            Assert.IsTrue(model.CatalogEmpty);
            Assert.IsTrue(model.InstalledEmpty);
            Assert.IsFalse(window.FindControl<Control>("MarketplaceActivity")!.IsVisible);
            Assert.IsTrue(window.FindControl<Button>("CatalogRefresh")!.IsEffectivelyEnabled);
        }
        finally
        {
            manager.ListGate.TrySetResult();
            remote.ListGate.TrySetResult();
            window.Close();
        }
    });

    [TestMethod]
    [DataRow("en-US", false, "install")]
    [DataRow("ko-KR", true, "remove")]
    [DataRow("en-US", false, "update")]
    public Task PendingMutationShowsItsCapturedTargetAcrossTabsAndDisablesFurtherActions(
        string cultureName, bool dark, string operation) => OnUiThread(async () =>
    {
        using var culture = new UiCultureScope(cultureName);
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var state = new SkillManager { Items = [TrackedSkill()] };
        var manager = new ProgressSkillManager(state);
        var remote = new ProgressMarketplace(new Marketplace(state));
        using var model = new SkillMarketplaceWindowViewModel(manager, remote, new Messages());
        await model.LoadAsync();
        SelectProgressTarget(model, operation);
        var target = operation == "install" ? model.SelectedCatalogSkill!.Name : model.SelectedInstalledSkill!.Name;
        var gate = ProgressGateFor(manager, remote, operation);
        var window = ProgressWindow(model);
        var task = ExecuteProgressMutation(model, operation);
        try
        {
            await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(model.HasLocalActivity);
            Assert.Contains(target, model.LocalActivityText);
            var verb = cultureName == "ko-KR"
                ? operation switch { "install" => "설치", "update" => "업데이트", _ => "제거" }
                : operation switch { "install" => "Installing", "update" => "Updating", _ => "Removing" };
            Assert.Contains(verb, model.LocalActivityText);
            Assert.IsFalse(model.ImportSkillCommand.CanExecute(null));
            Assert.IsFalse(model.RefreshInstalledCommand.CanExecute(null));
            Assert.IsFalse(model.InstallSkillCommand.CanExecute(null));
            Assert.IsFalse(model.UpdateInstalledSkillCommand.CanExecute(null));
            Assert.IsFalse(model.RemoveSkillCommand.CanExecute(null));
            var capturedActivity = model.LocalActivityText;
            model.SelectedCatalogSkill = model.CatalogSkills.FirstOrDefault(x => x.Id == "another");
            model.SelectedInstalledSkill = null;
            var tabs = window.FindControl<TabControl>("MarketplaceTabs")!;
            foreach (var tab in new[] { 0, 1, 2 })
            {
                tabs.SelectedIndex = tab;
                Dispatcher.UIThread.RunJobs();
                AssertProgressVisible(window, model);
                var activity = window.FindControl<TextBlock>("LocalActivity")!;
                Assert.IsTrue(activity.IsEffectivelyVisible);
                Assert.AreEqual(capturedActivity, activity.Text);
            }
            tabs.SelectedIndex = operation == "install" ? 0 : 1;
            Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(window.FindControl<Button>(operation == "install" ? "CatalogInstall" : "InstalledRemove")!.IsEffectivelyEnabled);
            await SaveProgressAsync(window, $"marketplace-progress-{cultureName}-{(dark ? "dark" : "light")}-{operation}.png");
            gate.Release.TrySetResult();
            await task.WaitAsync(TimeSpan.FromSeconds(5));
            Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(model.IsBusy);
            Assert.IsFalse(model.HasLocalActivity);
            Assert.AreEqual(string.Empty, model.LocalActivityText);
            Assert.IsFalse(window.FindControl<Control>("MarketplaceActivity")!.IsVisible);
            if (operation == "install") Assert.IsTrue(state.Items.Any(x => x.Id == "available"));
            if (operation == "remove") Assert.IsEmpty(state.Items);
            if (operation == "update") Assert.AreEqual(new string('b', 40), state.Items.Single().Source!.SourceRef);
        }
        finally { gate.Release.TrySetResult(); window.Close(); }
    });

    [TestMethod]
    [DataRow("install", "failure")]
    [DataRow("install", "cancel")]
    [DataRow("update", "failure")]
    [DataRow("update", "cancel")]
    [DataRow("remove", "failure")]
    [DataRow("remove", "cancel")]
    public Task MutationFailureOrCancellationClearsActivityAndRestoresLocalControls(string operation, string outcome) => OnUiThread(async () =>
    {
        using var culture = new UiCultureScope("en-US");
        var state = new SkillManager { Items = [TrackedSkill()] };
        var manager = new ProgressSkillManager(state);
        var remote = new ProgressMarketplace(new Marketplace(state));
        using var model = new SkillMarketplaceWindowViewModel(manager, remote, new Messages());
        await model.LoadAsync();
        SelectProgressTarget(model, operation);
        var gate = ProgressGateFor(manager, remote, operation);
        gate.Outcome = outcome;
        var task = ExecuteProgressMutation(model, operation);
        try
        {
            await gate.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(model.IsBusy);
            Assert.IsTrue(model.HasLocalActivity);
            gate.Release.TrySetResult();
            await task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(model.IsBusy);
            Assert.IsFalse(model.LocalBusy);
            Assert.IsFalse(model.HasLocalActivity);
            Assert.AreEqual(string.Empty, model.LocalActivityText);
            Assert.IsTrue(model.ImportSkillCommand.CanExecute(null));
            Assert.IsTrue(model.RefreshInstalledCommand.CanExecute(null));
            Assert.IsTrue(model.RemoveSkillCommand.CanExecute(null));
            Assert.HasCount(1, state.Items);
            Assert.AreEqual(new string('a', 40), state.Items.Single().Source!.SourceRef);
            Assert.IsFalse(string.IsNullOrWhiteSpace(model.LocalNotice));
            if (outcome == "cancel") Assert.AreEqual("Skill operation canceled.", model.LocalNotice);
        }
        finally { gate.Release.TrySetResult(); }
    });

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public Task ConcurrentCatalogAndRemovalKeepTheOtherActivityVisibleWhenEitherFinishes(bool catalogFirst) => OnUiThread(async () =>
    {
        var state = new SkillManager { Items = [TrackedSkill()] };
        var manager = new ProgressSkillManager(state);
        var catalog = new Marketplace(state);
        using var model = new SkillMarketplaceWindowViewModel(manager, new ProgressMarketplace(catalog), new Messages());
        await model.LoadAsync();
        SelectProgressTarget(model, "remove");
        var removal = model.RemoveSkillCommand.ExecuteAsync(null);
        await manager.Removal.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        catalog.ListGate = NewProgressCompletion();
        var refresh = model.RefreshCatalogCommand.ExecuteAsync(null);
        var window = ProgressWindow(model);
        try
        {
            await Until(() => model.CatalogBusy && model.LocalBusy);
            AssertProgressVisible(window, model);
            if (catalogFirst)
            {
                catalog.ListGate.TrySetResult();
                await refresh.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.IsFalse(model.CatalogBusy);
                Assert.IsTrue(model.LocalBusy);
                Dispatcher.UIThread.RunJobs();
                Assert.IsTrue(window.FindControl<TextBlock>("LocalActivity")!.IsEffectivelyVisible);
                AssertProgressVisible(window, model);
                manager.Removal.Release.TrySetResult();
            }
            else
            {
                manager.Removal.Release.TrySetResult();
                await removal.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.IsFalse(model.LocalBusy);
                Assert.IsTrue(model.CatalogBusy);
                Assert.IsFalse(model.HasLocalActivity);
                Dispatcher.UIThread.RunJobs();
                Assert.IsTrue(window.FindControl<TextBlock>("CatalogActivity")!.IsEffectivelyVisible);
                AssertProgressVisible(window, model);
                catalog.ListGate.TrySetResult();
            }
            await Task.WhenAll(removal, refresh).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(model.IsBusy);
        }
        finally { manager.Removal.Release.TrySetResult(); catalog.ListGate.TrySetResult(); window.Close(); }
    });

    [TestMethod]
    [DataRow("success")]
    [DataRow("failure")]
    [DataRow("cancel")]
    public Task RuntimeCheckKeepsCommonProgressVisibleAndClearsItOnEveryOutcome(string outcome) => OnUiThread(async () =>
    {
        var manager = new SkillManager();
        var runtimes = new ProgressRuntimes();
        runtimes.Check.Outcome = outcome;
        using var model = new SkillMarketplaceWindowViewModel(manager, new Marketplace(manager), new Messages(), runtimes);
        var load = model.LoadAsync();
        var window = ProgressWindow(model);
        try
        {
            await runtimes.Check.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Until(() => !model.CatalogBusy && !model.LocalBusy);
            Assert.IsTrue(model.SkillRuntimeBusy);
            Assert.IsFalse(model.HasLocalActivity);
            AssertProgressVisible(window, model);
            Assert.IsTrue(window.FindControl<TextBlock>("RuntimeActivity")!.IsEffectivelyVisible);
            Assert.AreEqual(model.SkillRuntimeNotice, window.FindControl<TextBlock>("RuntimeActivity")!.Text);
            runtimes.Check.Release.TrySetResult();
            await load.WaitAsync(TimeSpan.FromSeconds(5));
            Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(model.IsBusy);
            Assert.IsFalse(window.FindControl<Control>("MarketplaceActivity")!.IsVisible);
        }
        finally { runtimes.Check.Release.TrySetResult(); window.Close(); }
    });

    [TestMethod]
    public Task DelayedDiscoveryFailureClearsProgressAndRestoresEmptyStateAndRefresh() => OnUiThread(async () =>
    {
        var manager = new SkillManager { Items = [], ListGate = NewProgressCompletion() };
        var remote = new Marketplace(manager) { Failure = true, ListGate = NewProgressCompletion() };
        using var model = new SkillMarketplaceWindowViewModel(manager, remote, new Messages());
        var load = model.LoadAsync();
        var window = ProgressWindow(model);
        try
        {
            await Task.WhenAll(manager.ListStarted.Task, remote.Started.Task).WaitAsync(TimeSpan.FromSeconds(5));
            AssertProgressVisible(window, model);
            Assert.IsFalse(model.CatalogEmpty);
            Assert.IsFalse(model.InstalledEmpty);
            manager.ListGate.TrySetException(new ManagedAiException(AiFailureCode.SkillInvalid));
            remote.ListGate.TrySetResult();
            await load.WaitAsync(TimeSpan.FromSeconds(5));
            Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(model.IsBusy);
            Assert.IsFalse(model.HasLocalActivity);
            Assert.IsTrue(model.CatalogEmpty);
            Assert.IsTrue(model.InstalledEmpty);
            Assert.IsTrue(model.RefreshCatalogCommand.CanExecute(null));
            Assert.IsTrue(model.RefreshInstalledCommand.CanExecute(null));
            Assert.IsFalse(window.FindControl<Control>("MarketplaceActivity")!.IsVisible);
        }
        finally { manager.ListGate.TrySetResult(); remote.ListGate.TrySetResult(); window.Close(); }
    });

    [TestMethod]
    public Task ClosingDuringDiscoveryCancelsPendingCallsAndClearsAllBusyIndicators() => OnUiThread(async () =>
    {
        var manager = new SkillManager { ListGate = NewProgressCompletion() };
        var remote = new Marketplace(manager) { ListGate = NewProgressCompletion() };
        using var model = new SkillMarketplaceWindowViewModel(manager, remote, new Messages());
        var load = model.LoadAsync();
        try
        {
            await Task.WhenAll(manager.ListStarted.Task, remote.Started.Task).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(model.IsBusy);
            model.Dispose();
            await load.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(model.IsBusy);
            Assert.IsFalse(model.CatalogBusy);
            Assert.IsFalse(model.LocalBusy);
            Assert.IsFalse(model.HasLocalActivity);
            Assert.AreEqual(string.Empty, model.LocalActivityText);
        }
        finally { manager.ListGate.TrySetResult(); remote.ListGate.TrySetResult(); }
    });

    private static TaskCompletionSource NewProgressCompletion() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static SkillMarketplaceWindow ProgressWindow(SkillMarketplaceWindowViewModel model)
    {
        var window = new SkillMarketplaceWindow { DataContext = model, Width = 760, Height = 560 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static async Task SaveProgressAsync(Window window, string filename)
    {
        // Advance composition time beyond the ring's initial zero-length arc before capturing.
        for (var frame = 0; frame < 6; frame++)
        {
            await Task.Delay(50);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Save(window, filename);
    }

    private static void AssertProgressVisible(SkillMarketplaceWindow window, SkillMarketplaceWindowViewModel model)
    {
        Dispatcher.UIThread.RunJobs();
        Assert.IsTrue(model.IsBusy);
        var activity = window.FindControl<Control>("MarketplaceActivity")!;
        Assert.IsTrue(activity.IsEffectivelyVisible);
        var progress = window.FindControl<ProgressRing>("MarketplaceProgress");
        Assert.IsNotNull(progress);
        Assert.IsTrue(progress.IsIndeterminate);
        Assert.IsTrue(progress.IsActive);
        Assert.IsNotNull(progress.Template, "The progress indicator must have its product theme template.");
        Assert.IsTrue(progress.Bounds.Width > 0 && progress.Bounds.Height > 0,
            "The progress indicator must occupy a visible layout area.");
        var animatedVisual = progress.GetVisualDescendants().OfType<ProgressRingAnimatedVisual>().SingleOrDefault();
        Assert.IsNotNull(animatedVisual, "The progress template must create its animated visual.");
        Assert.IsTrue(animatedVisual.IsEffectivelyVisible);
        Assert.IsTrue(animatedVisual.Bounds.Width > 0 && animatedVisual.Bounds.Height > 0,
            "The animated visual must occupy a visible layout area.");
        var position = activity.TranslatePoint(new Point(0, 0), window);
        Assert.IsNotNull(position);
        Assert.IsTrue(position.Value.Y >= 0 && position.Value.Y + activity.Bounds.Height < window.Bounds.Height,
            "Common progress must remain inside the minimum-size window.");
    }

    private static void SelectProgressTarget(SkillMarketplaceWindowViewModel model, string operation)
    {
        model.SelectedInstalledSkill = model.InstalledSkills.Single();
        model.SelectedCatalogSkill = model.CatalogSkills.Single(x => x.Id == (operation == "install" ? "available" : "FIXTURE"));
    }

    private static Task ExecuteProgressMutation(SkillMarketplaceWindowViewModel model, string operation) => operation switch
    {
        "install" => model.InstallSkillCommand.ExecuteAsync(null),
        "update" => model.UpdateInstalledSkillCommand.ExecuteAsync(null),
        _ => model.RemoveSkillCommand.ExecuteAsync(null)
    };

    private static ProgressOperation ProgressGateFor(ProgressSkillManager manager, ProgressMarketplace remote, string operation)
        => operation switch { "install" => remote.Installation, "update" => remote.Update, _ => manager.Removal };

    private sealed class ProgressOperation
    {
        public TaskCompletionSource Started { get; } = NewProgressCompletion();
        public TaskCompletionSource Release { get; } = NewProgressCompletion();
        public string Outcome = "success";
        public async Task WaitAsync(CancellationToken token)
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(token);
            if (Outcome == "failure") throw new ManagedAiException(AiFailureCode.SkillInvalid);
            if (Outcome == "cancel") throw new OperationCanceledException();
        }
    }

    private sealed class ProgressSkillManager(SkillManager inner) : IManagedAiSkillManager
    {
        public ProgressOperation Removal { get; } = new();
        public string StorageDirectory => inner.StorageDirectory;
        public Task<IReadOnlyList<AiSkill>> ListAsync(CancellationToken token) => inner.ListAsync(token);
        public Task<IReadOnlyList<AiSkill>> ImportAsync(string source, CancellationToken token) => inner.ImportAsync(source, token);
        public Task<IReadOnlyList<AiSkill>> SetEnabledAsync(string id, bool enabled, CancellationToken token) => inner.SetEnabledAsync(id, enabled, token);
        public async Task<IReadOnlyList<AiSkill>> RemoveAsync(string id, CancellationToken token)
        { await Removal.WaitAsync(token); return await inner.RemoveAsync(id, token); }
    }

    private sealed class ProgressMarketplace(Marketplace inner) : IManagedAiSkillMarketplace
    {
        public ProgressOperation Installation { get; } = new();
        public ProgressOperation Update { get; } = new();
        public Task<IReadOnlyList<SkillCatalogEntry>> ListAsync(CancellationToken token) => inner.ListAsync(token);
        public async Task<IReadOnlyList<AiSkill>> InstallAsync(string id, CancellationToken token)
        { await Installation.WaitAsync(token); return await inner.InstallAsync(id, token); }
        public async Task<IReadOnlyList<AiSkill>> UpdateAsync(string id, CancellationToken token)
        { await Update.WaitAsync(token); return await inner.UpdateAsync(id, token); }
    }

    private sealed class ProgressRuntimes : IManagedAiScriptRuntimeManager
    {
        public ProgressOperation Check { get; } = new();
        public async Task<AiScriptRuntimeStatus> GetStatusAsync(CancellationToken token)
        { await Check.WaitAsync(token); return RuntimeStatus(true, true); }
        public Task<AiScriptRuntimeStatus> EnsureInstalledAsync(CancellationToken token) => GetStatusAsync(token);
    }
}
