using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using TableCloth.Dialogs;
using TableCloth.ManagedAi;
using TableCloth.ViewModels;

namespace TableCloth.Test;

public sealed partial class SkillMarketplaceTests
{
    [TestMethod]
    [DataRow(true, true, false, false)]
    [DataRow(false, true, true, false)]
    [DataRow(true, false, true, false)]
    [DataRow(null, true, false, true)]
    [DataRow(true, null, false, true)]
    [DataRow(null, null, false, true)]
    [DataRow(false, null, true, true)]
    public Task RuntimeWarningRequiresBothIsolatedLongPathChecksToPass(bool? python, bool? node, bool limited, bool unknown) => OnUiThread(async () =>
    {
        using var culture = new UiCultureScope("en-US");
        var manager = new SkillManager();
        var runtimes = new ScriptRuntimes { Status = RuntimeStatus(python, node) };
        using var model = new SkillMarketplaceWindowViewModel(manager, new Marketplace(manager), new Messages(), runtimes);
        await model.LoadAsync();
        Assert.AreEqual(limited, model.HasSkillRuntimeLimits);
        Assert.AreEqual(unknown, model.HasSkillRuntimeUnknown);
        Assert.AreEqual(limited || unknown, model.HasSkillRuntimeWarning);
        Assert.AreEqual("3.14.9", model.PythonRuntimeVersion);
        Assert.AreEqual("24.22.1", model.NodeJsRuntimeVersion);
        Assert.AreEqual(1, runtimes.StatusCalls);
        if (limited || unknown) Assert.Contains("may be unavailable", model.SkillRuntimeWarning);
    });

    [TestMethod]
    public Task FailedRefreshDiscardsEarlierPassingChecks() => OnUiThread(async () =>
    {
        using var culture = new UiCultureScope("en-US");
        var manager = new SkillManager();
        var runtimes = new ScriptRuntimes { Status = RuntimeStatus(true, true) };
        using var model = new SkillMarketplaceWindowViewModel(manager, new Marketplace(manager), new Messages(), runtimes);
        await model.LoadAsync();
        Assert.IsFalse(model.HasSkillRuntimeWarning);
        runtimes.Fail = true;
        await model.RefreshSkillRuntimeStatusCommand.ExecuteAsync(null);
        Assert.IsTrue(model.HasSkillRuntimeUnknown);
        Assert.IsTrue(model.HasSkillRuntimeWarning);
        Assert.AreEqual("Unverified", model.PythonLongPathStatus);
        Assert.AreEqual("Not checked", model.PythonRuntimeVersion);
        Assert.Contains("Could not verify", model.SkillRuntimeNotice);
    });

    [TestMethod]
    public Task RuntimeRepairUsesDedicatedManagerAndRefreshesVersionsAndLimits() => OnUiThread(async () =>
    {
        using var culture = new UiCultureScope("en-US");
        var manager = new SkillManager();
        var runtimes = new ScriptRuntimes { Status = RuntimeStatus(null, null) with
        {
            Python = new("Python", "3.14.9", null, false, "RuntimeNotInstalled"),
            Node = new("Node.js", "24.22.1", null, false, "RuntimeNotInstalled")
        } };
        using var model = new SkillMarketplaceWindowViewModel(manager, new Marketplace(manager), new Messages(), runtimes);
        await model.LoadAsync();
        Assert.AreEqual("Not installed", model.PythonRuntimeVersion);
        Assert.IsTrue(model.HasSkillRuntimeWarning);
        runtimes.Status = RuntimeStatus(true, true);
        await model.InstallSkillRuntimesCommand.ExecuteAsync(null);
        Assert.AreEqual(1, runtimes.InstallCalls);
        Assert.IsFalse(model.HasSkillRuntimeWarning);
        Assert.AreEqual("3.14.9", model.PythonRuntimeVersion);
        Assert.AreEqual("24.22.1", model.NodeJsRuntimeVersion);
        Assert.IsFalse(model.LocalBusy);
        Assert.IsTrue(model.RefreshSkillRuntimeStatusCommand.CanExecute(null));
    });

    [TestMethod]
    public Task MissingRuntimeServiceKeepsUnknownWarningAndDisablesActions() => OnUiThread(async () =>
    {
        var manager = new SkillManager();
        using var model = new SkillMarketplaceWindowViewModel(manager, new Marketplace(manager), new Messages());
        await model.LoadAsync();
        Assert.IsTrue(model.HasSkillRuntimeWarning);
        Assert.IsTrue(model.HasSkillRuntimeUnknown);
        Assert.IsFalse(model.InstallSkillRuntimesCommand.CanExecute(null));
        Assert.IsFalse(model.RefreshSkillRuntimeStatusCommand.CanExecute(null));
    });

    [TestMethod]
    [DataRow("en-US", false)]
    [DataRow("ko-KR", true)]
    public Task RuntimeTabShowsUnverifiedWarningAndRepairWithinMinimumWindow(string cultureName, bool dark) => OnUiThread(async () =>
    {
        using var culture = new UiCultureScope(cultureName);
        Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var manager = new SkillManager();
        var runtimes = new ScriptRuntimes { Status = RuntimeStatus(true, null) };
        using var model = new SkillMarketplaceWindowViewModel(manager, new Marketplace(manager), new Messages(), runtimes);
        await model.LoadAsync();
        var window = new SkillMarketplaceWindow { DataContext = model, Width = 760, Height = 560 };
        try
        {
            window.Show();
            window.FindControl<TabControl>("MarketplaceTabs")!.SelectedIndex = 2;
            Dispatcher.UIThread.RunJobs();
            Assert.IsTrue(window.FindControl<Border>("SkillRuntimeWarning")!.IsEffectivelyVisible);
            Assert.Contains(cultureName == "ko-KR" ? "기능이 제한될 수" : "may be unavailable",
                window.FindControl<TextBlock>("SkillRuntimeWarningText")!.Text!);
            Assert.AreEqual("3.14.9", window.FindControl<TextBlock>("PythonRuntimeVersion")!.Text);
            foreach (var name in new[] { "SkillRuntimeInstall", "SkillRuntimeRefresh" })
            {
                var button = window.FindControl<Button>(name)!;
                Assert.IsTrue(button.IsEffectivelyEnabled);
                var point = button.TranslatePoint(new Point(0, 0), window);
                Assert.IsNotNull(point);
                Assert.IsLessThan(window.Bounds.Height - 45, point.Value.Y + button.Bounds.Height);
            }
            Save(window, $"marketplace-{cultureName}-runtimes-{(dark ? "dark" : "light")}.png");
            runtimes.Status = RuntimeStatus(true, true);
            await model.RefreshSkillRuntimeStatusCommand.ExecuteAsync(null);
            Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(window.FindControl<Border>("SkillRuntimeWarning")!.IsVisible);
        }
        finally { window.Close(); }
    });

    private static AiScriptRuntimeStatus RuntimeStatus(bool? python, bool? node) => new(
        new("Python", "3.14.9", "3.14.9", true, null),
        new("Node.js", "24.22.1", "24.22.1", true, null),
        new(python, node, null));

    private sealed class ScriptRuntimes : IManagedAiScriptRuntimeManager
    {
        public AiScriptRuntimeStatus Status = RuntimeStatus(true, true);
        public bool Fail;
        public int StatusCalls;
        public int InstallCalls;
        public Task<AiScriptRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken)
        {
            StatusCalls++;
            cancellationToken.ThrowIfCancellationRequested();
            if (Fail) throw new IOException("Fixture check failure");
            return Task.FromResult(Status);
        }
        public Task<AiScriptRuntimeStatus> EnsureInstalledAsync(CancellationToken cancellationToken)
        {
            InstallCalls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Status);
        }
    }
}
