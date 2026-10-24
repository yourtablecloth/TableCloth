using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Globalization;
using TableCloth.Components;
using TableCloth.Dialogs;
using TableCloth.ManagedAi;
using TableCloth.Models;
using TableCloth.Models.Configuration;
using TableCloth.ViewModels;

namespace TableCloth.Test;

[TestClass, DoNotParallelize]
public sealed class OptionsSkillTests
{
    [TestMethod]
    public async Task OptionsLoadAndSkillTabSelectionDoNotLoadSkillsOrCatalog()
    {
        var skills = new SkillManager();
        var marketplace = new Marketplace();
        var preferences = new Preferences();
        var viewModel = new OptionsWindowViewModel(preferences, null!, new Messages(), new Locations(), new TaskFactory(), skills, marketplace);
        viewModel.SetInitialTab(OptionsTabKeys.AiSkills);
        await viewModel.OptionsWindowLoadedCommand.ExecuteAsync(null);
        viewModel.SetInitialTab(OptionsTabKeys.Session);
        viewModel.SetInitialTab(OptionsTabKeys.AiSkills);
        Assert.AreEqual(0, skills.ListCount);
        Assert.AreEqual(0, marketplace.ListCount);
        Assert.AreEqual(1, preferences.LoadCount);
        Assert.AreEqual(0, preferences.SaveCount);
        Assert.IsTrue(viewModel.CanOpenSkillMarketplace);
        using var management = viewModel.CreateSkillMarketplaceViewModel();
        Assert.IsNotNull(management);
        Assert.AreEqual(0, skills.ListCount);
        Assert.AreEqual(0, marketplace.ListCount);
    }

    [TestMethod]
    [DataRow("ko-KR", false)]
    [DataRow("en-US", true)]
    public async Task SkillOptionsShowOnlyGuidanceAndMarketplaceEntryAtMinimumSize(string cultureName, bool dark)
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            using var culture = new UiCultureScope(cultureName);
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var skills = new SkillManager();
            var marketplace = new Marketplace();
            var viewModel = new OptionsWindowViewModel(new Preferences(), null!, new Messages(), new Locations(), new TaskFactory(), skills, marketplace);
            viewModel.SetInitialTab(OptionsTabKeys.AiSkills);
            var window = new OptionsWindow { DataContext = viewModel, Width = 600, Height = 480 };
            try
            {
                window.Show();
                await Task.Yield();
                Dispatcher.UIThread.RunJobs();
                var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
                Assert.AreEqual(7, tabs.SelectedIndex);
                Assert.AreEqual(ManagedAiText.SkillsTabTitle, ((TabItem)tabs.SelectedItem!).Header);
                Assert.AreEqual(ManagedAiText.SkillsTabDescription, window.FindControl<TextBlock>("AiSkillsGuide")!.Text);
                var button = window.FindControl<Button>("AiSkillMarketplace")!;
                Assert.AreEqual(ManagedAiText.SkillsMarketplace, button.Content);
                Assert.IsTrue(button.IsEffectivelyEnabled);
                foreach (var removed in new[] { "AiSkillList", "AiSkillSummary", "AiSkillAdd", "AiSkillToggle", "AiSkillRemove", "AiSkillRefresh", "AiSkillFolder", "AiSkillNotice" })
                    Assert.IsNull(window.FindControl<Control>(removed));
                var position = button.TranslatePoint(new Point(0, 0), window);
                Assert.IsNotNull(position);
                Assert.IsLessThan(window.ClientSize.Height - 50, position.Value.Y + button.Bounds.Height);
                Assert.AreEqual(0, skills.ListCount);
                Assert.AreEqual(0, marketplace.ListCount);
                var screenshot = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts", $"options-ai-skills-guide-{cultureName}-600-{(dark ? "dark" : "light")}.png");
                Directory.CreateDirectory(Path.GetDirectoryName(screenshot)!);
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using var frame = window.CaptureRenderedFrame();
                Assert.IsNotNull(frame);
                frame.Save(screenshot);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task MarketplaceEntryOpensCatalogAsModalAndDoesNotReloadSkillsAfterClosing()
    {
        await HeadlessTestSession.Shared.Dispatch<bool>(async () =>
        {
            var skills = new SkillManager();
            var marketplace = new Marketplace();
            var viewModel = new OptionsWindowViewModel(new Preferences(), null!, new Messages(), new Locations(), new TaskFactory(), skills, marketplace);
            viewModel.SetInitialTab(OptionsTabKeys.AiSkills);
            var window = new OptionsWindow { DataContext = viewModel };
            SkillMarketplaceWindow? child = null;
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                window.FindControl<Button>("AiSkillMarketplace")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Until(() => window.OwnedWindows.OfType<SkillMarketplaceWindow>().Any());
                child = window.OwnedWindows.OfType<SkillMarketplaceWindow>().Single();
                Assert.AreSame(window, child.Owner);
                Assert.AreEqual(0, child.FindControl<TabControl>("MarketplaceTabs")!.SelectedIndex);
                await Until(() => skills.ListCount == 1 && marketplace.ListCount == 1
                    && child.DataContext is SkillMarketplaceWindowViewModel { LocalBusy: false, CatalogBusy: false });
                child.Close();
                await Task.Yield();
                Dispatcher.UIThread.RunJobs();
                Assert.AreEqual(1, skills.ListCount);
                Assert.AreEqual(1, marketplace.ListCount);
            }
            finally { child?.Close(); window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    private static async Task Until(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!ready() && DateTime.UtcNow < deadline) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
        Assert.IsTrue(ready(), "The marketplace did not reach its expected state.");
    }

    [TestMethod]
    public async Task IdleShutdownAppearsInSandboxSessionTab()
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            using var culture = new UiCultureScope("ko-KR");
            var viewModel = new DesignViewModel();
            viewModel.SetInitialTab(OptionsTabKeys.Session);
            var window = new OptionsWindow { DataContext = viewModel };
            try
            {
                window.Show();
                await Task.Yield();
                var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
                Assert.AreEqual(4, tabs.SelectedIndex);
                Assert.AreEqual("샌드박스 세션", ((TabItem)tabs.SelectedItem!).Header);
                var idle = window.GetVisualDescendants().OfType<CheckBox>()
                    .Single(x => Equals(x.Content, "자리 비움 시 자동 종료"));
                Assert.IsFalse(idle.IsChecked ?? false);
                idle.IsChecked = true;
                Assert.IsTrue(viewModel.EnableIdleAutoLogout);
                Assert.IsTrue(window.GetVisualDescendants().OfType<ComboBox>().Single().IsEnabled);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    private sealed class UiCultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentUICulture;

        public UiCultureScope(string name) => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);

        public void Dispose() => CultureInfo.CurrentUICulture = _previous;
    }

    private sealed class DesignViewModel : OptionsWindowViewModel { }

    private sealed class SkillManager : IManagedAiSkillManager
    {
        private int _listCount;
        public int ListCount => _listCount;
        public string StorageDirectory => "C:\\fixture-skills";
        public Task<IReadOnlyList<AiSkill>> ListAsync(CancellationToken token)
        { Interlocked.Increment(ref _listCount); return Task.FromResult<IReadOnlyList<AiSkill>>([]); }
        public Task<IReadOnlyList<AiSkill>> ImportAsync(string sourceDirectory, CancellationToken token)
            => throw new AssertFailedException("Unexpected import");
        public Task<IReadOnlyList<AiSkill>> SetEnabledAsync(string id, bool enabled, CancellationToken token)
            => throw new AssertFailedException("Unexpected toggle");
        public Task<IReadOnlyList<AiSkill>> RemoveAsync(string id, CancellationToken token)
            => throw new AssertFailedException("Unexpected remove");
    }

    private sealed class Marketplace : IManagedAiSkillMarketplace
    {
        private int _listCount;
        public int ListCount => _listCount;
        public Task<IReadOnlyList<SkillCatalogEntry>> ListAsync(CancellationToken token)
        { Interlocked.Increment(ref _listCount); return Task.FromResult<IReadOnlyList<SkillCatalogEntry>>([]); }
        public Task<IReadOnlyList<AiSkill>> InstallAsync(string id, CancellationToken token)
            => throw new AssertFailedException("Unexpected install");
    }

    private sealed class Preferences : IPreferencesManager
    {
        public int LoadCount;
        public int SaveCount;
        public PreferenceSettings GetDefaultPreferences() => new();
        public Task<PreferenceSettings?> LoadPreferencesAsync(CancellationToken token = default)
        { LoadCount++; return Task.FromResult<PreferenceSettings?>(new()); }
        public Task SavePreferencesAsync(PreferenceSettings preferences, CancellationToken token = default)
        { SaveCount++; return Task.CompletedTask; }
    }

    private sealed class Locations : ISharedLocations
    {
        public string GetEffectiveDataDirectoryPath(string? configuredPath) => configuredPath ?? "C:\\Data";
        public bool IsMountableDataDirectory(string? path) => true;
        public string ExecutableDirectoryPath => throw new NotSupportedException();
        public string ImagesZipFilePath => throw new NotSupportedException();
        public string CatalogCacheFilePath => throw new NotSupportedException();
        public string AppDataDirectoryPath => throw new NotSupportedException();
        public string ApplicationLogPath => throw new NotSupportedException();
        public string ExecutableFilePath => throw new NotSupportedException();
        public string PreferencesFilePath => throw new NotSupportedException();
        public string DefaultDataDirectoryPath => "C:\\Data";
        public string GetImageDirectoryPath() => throw new NotSupportedException();
        public string GetImageFilePath(string serviceId) => throw new NotSupportedException();
        public string GetIconFilePath(string serviceId) => throw new NotSupportedException();
        public string GetTempPath() => throw new NotSupportedException();
        public string GetHelpFilePath() => throw new NotSupportedException();
        public string GetSandboxAppStagingPath(string sandboxStagingDirectory) => throw new NotSupportedException();
    }

    private sealed class Messages : IAppMessageBox
    {
        public int Questions;
        public AppMessageBoxResult DisplayQuestion(string message, AppMessageBoxButton buttons, AppMessageBoxResult answer)
        { Questions++; return AppMessageBoxResult.Yes; }
        public AppMessageBoxResult DisplayInfo(string message, AppMessageBoxButton buttons) => throw new NotSupportedException();
        public AppMessageBoxResult DisplayError(Exception? reason, bool critical, string file, string member, int line)
            => throw new NotSupportedException();
        public AppMessageBoxResult DisplayError(string? message, bool critical, string file, string member, int line)
            => throw new NotSupportedException();
    }
}
