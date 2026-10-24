using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using System.Globalization;
using TableCloth.Components;
using TableCloth.Dialogs;
using TableCloth.ManagedAi;
using TableCloth.Models;
using TableCloth.ViewModels;

namespace TableCloth.Test;

[TestClass, DoNotParallelize]
public sealed class SkillMarketplaceTests
{
    [TestMethod]
    public async Task InvalidInstalledSkillCanBeDisabledAndRemovedButCannotBeEnabled()
    {
        using var culture = new UiCultureScope("en-US");
        var manager = new SkillManager();
        manager.Items = manager.Items.Select(x => x with { IsValid = false }).ToArray();
        using var model = new SkillMarketplaceWindowViewModel(manager, new Marketplace(manager), new Messages());
        await model.LoadAsync();
        model.SelectedInstalledSkill = model.InstalledSkills.Single();
        Assert.AreEqual("Invalid SKILL.md", model.SelectedInstalledSkill.StateLabel);
        Assert.IsTrue(model.ToggleSkillCommand.CanExecute(null));
        await model.ToggleSkillCommand.ExecuteAsync(null);
        Assert.IsFalse(manager.Items.Single().Enabled);
        Assert.IsFalse(model.ToggleSkillCommand.CanExecute(null));
        await model.ToggleSkillCommand.ExecuteAsync(null);
        Assert.IsFalse(manager.Items.Single().Enabled);
        Assert.IsTrue(model.OpenSelectedFolderCommand.CanExecute(null));
        Assert.IsTrue(model.RemoveSkillCommand.CanExecute(null));

        var options = new OptionsWindowViewModel(null!, null!, new Messages(), null!, new TaskFactory(), manager);
        await options.RefreshSkillListCommand.ExecuteAsync(null);
        options.SelectedAiSkill = options.AiSkills.Single();
        Assert.IsFalse(options.CanToggleAiSkill);
        await options.ToggleAiSkillCommand.ExecuteAsync(null);
        Assert.IsFalse(manager.Items.Single().Enabled);

        await model.RemoveSkillCommand.ExecuteAsync(null);
        Assert.IsEmpty(manager.Items);
    }

    [TestMethod]
    public async Task RemoteFailureKeepsInstalledManagementAvailable()
    {
        using var culture = new UiCultureScope("en-US");
        var manager = new SkillManager();
        var remote = new Marketplace(manager) { Failure = true };
        var messages = new Messages();
        using var model = new SkillMarketplaceWindowViewModel(manager, remote, messages);
        await model.LoadAsync();
        Assert.Contains("continue managing installed", model.CatalogNotice);
        model.SelectedInstalledSkill = model.InstalledSkills.Single();
        Assert.IsTrue(model.ToggleSkillCommand.CanExecute(null));
        await model.ToggleSkillCommand.ExecuteAsync(null);
        Assert.IsFalse(manager.Items.Single().Enabled);
        await model.ImportFromDirectoryAsync("fixture-import");
        Assert.HasCount(2, model.InstalledSkills);
        model.SelectedInstalledSkill = model.InstalledSkills.Single(x => x.Id == "imported");
        messages.Answer = AppMessageBoxResult.No;
        await model.RemoveSkillCommand.ExecuteAsync(null);
        Assert.HasCount(2, manager.Items);
        messages.Answer = AppMessageBoxResult.Yes;
        await model.RemoveSkillCommand.ExecuteAsync(null);
        Assert.HasCount(1, manager.Items);
        Assert.AreEqual(2, messages.Questions);
    }

    [TestMethod]
    public async Task ReviewGatesAndFiltersKeepSelectionAndInstalledIdsConsistent()
    {
        var manager = new SkillManager();
        var remote = new Marketplace(manager);
        using var model = new SkillMarketplaceWindowViewModel(manager, remote, new Messages());
        await model.LoadAsync();
        Assert.IsTrue(model.CatalogSkills.Single(x => x.Id == "FIXTURE").Installed);
        model.SelectedCatalogSkill = model.CatalogSkills.Single(x => x.Id == "pending");
        Assert.IsFalse(model.InstallSkillCommand.CanExecute(null));
        Assert.IsTrue(model.OpenSourceCommand.CanExecute(null));
        await model.InstallSkillCommand.ExecuteAsync(null);
        Assert.AreEqual(0, remote.InstallCount);

        model.CatalogSearch = "NomaDamas";
        Assert.HasCount(2, model.CatalogSkills);
        Assert.AreEqual("pending", model.SelectedCatalogSkill!.Id);
        model.SelectedCategory = model.Categories.Single(x => x.Id == "Finance");
        Assert.HasCount(1, model.CatalogSkills);
        Assert.IsNull(model.SelectedCatalogSkill);
        model.SelectedCatalogSkill = model.CatalogSkills.Single();
        Assert.IsTrue(model.InstallSkillCommand.CanExecute(null));
        await model.InstallSkillCommand.ExecuteAsync(null);
        Assert.AreEqual(1, remote.InstallCount);
        Assert.IsTrue(model.SelectedCatalogSkill!.Installed);
        Assert.IsFalse(model.InstallSkillCommand.CanExecute(null));

        model.SelectedInstalledSkill = model.InstalledSkills.First();
        model.InstalledSearch = "no matches";
        Assert.IsTrue(model.InstalledEmpty);
        Assert.IsNull(model.SelectedInstalledSkill);
        Assert.IsFalse(model.RemoveSkillCommand.CanExecute(null));
        Assert.IsFalse(model.ToggleSkillCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task PendingRemoteRequestDoesNotBlockLocalActionsAndCloseCancelsIt()
    {
        var manager = new SkillManager();
        var remote = new Marketplace(manager) { WaitForCancellation = true };
        var model = new SkillMarketplaceWindowViewModel(manager, remote, new Messages());
        var load = model.LoadAsync();
        await remote.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await model.RefreshInstalledCommand.ExecuteAsync(null);
        Assert.IsTrue(model.CatalogBusy);
        Assert.IsTrue(model.ImportSkillCommand.CanExecute(null));
        model.SelectedInstalledSkill = model.InstalledSkills.Single();
        await model.ToggleSkillCommand.ExecuteAsync(null);
        Assert.IsFalse(manager.Items.Single().Enabled);
        model.Dispose();
        await load.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsFalse(model.RefreshCatalogCommand.CanExecute(null));
        Assert.IsFalse(model.RefreshInstalledCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task BusyLocalActionKeepsItsCapturedSelectionAndBlocksFurtherMutations()
    {
        var manager = new SkillManager();
        using var model = new SkillMarketplaceWindowViewModel(manager, new Marketplace(manager), new Messages());
        await model.LoadAsync();
        manager.ToggleCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        model.SelectedInstalledSkill = model.InstalledSkills.Single();
        var toggle = model.ToggleSkillCommand.ExecuteAsync(null);
        await manager.ToggleStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsTrue(model.LocalBusy);
        Assert.IsFalse(model.ImportSkillCommand.CanExecute(null));
        Assert.IsFalse(model.RemoveSkillCommand.CanExecute(null));
        Assert.IsFalse(model.OpenSelectedFolderCommand.CanExecute(null));
        model.SelectedInstalledSkill = null;
        manager.ToggleCompletion.SetResult();
        await toggle;
        Assert.IsFalse(manager.Items.Single().Enabled);
        Assert.IsNull(model.SelectedInstalledSkill);
    }

    [TestMethod]
    [DataRow("https://github.com/NomaDamas/k-skill", true)]
    [DataRow("https://github.com.evil.example/NomaDamas/k-skill", false)]
    [DataRow("https://user@github.com/NomaDamas/k-skill", false)]
    [DataRow("http://github.com/NomaDamas/k-skill", false)]
    [DataRow("file:///C:/Windows", false)]
    public void UpstreamLinksAllowOnlyGithubRepositories(string repository, bool expected)
    {
        var entry = Entry("source") with { Repository = repository };
        Assert.AreEqual(expected, SkillMarketplaceWindowViewModel.TryGetSourceUri(entry, out _));
    }

    [TestMethod]
    public void RootSkillSourceOpensThePinnedRepositoryRevision()
    {
        var entry = Entry("source") with { SourcePath = ".", SourceRef = new string('a', 40) };
        Assert.IsTrue(SkillMarketplaceWindowViewModel.TryGetSourceUri(entry, out var uri));
        Assert.AreEqual(entry.Repository + "/tree/" + entry.SourceRef, uri.AbsoluteUri);
    }

    [TestMethod]
    public void LicenseLinksRequireTheSameRepositoryAndPinnedRevision()
    {
        var entry = Entry("source") with { SourceRef = new string('a', 40) };
        entry = entry with { LicenseSource = entry.Repository + "/blob/" + entry.SourceRef + "/LICENSE" };
        Assert.IsTrue(SkillMarketplaceWindowViewModel.TryGetLicenseUri(entry, out _));
        Assert.IsFalse(SkillMarketplaceWindowViewModel.TryGetLicenseUri(entry with { LicenseSource = "https://github.com/other/repo/blob/" + entry.SourceRef + "/LICENSE" }, out _));
        Assert.IsFalse(SkillMarketplaceWindowViewModel.TryGetLicenseUri(entry with { LicenseSource = entry.Repository + "/blob/main/LICENSE" }, out _));
        Assert.IsFalse(SkillMarketplaceWindowViewModel.TryGetLicenseUri(entry with { LicenseSource = "file:///C:/Windows" }, out _));
    }

    [TestMethod]
    public void FolderOpenRequiresARealDirectoryInsideTheDedicatedRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "tablecloth-marketplace-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "skill"));
            Assert.IsTrue(SkillMarketplaceWindowViewModel.IsManagedFolder(root, root));
            Assert.IsTrue(SkillMarketplaceWindowViewModel.IsManagedFolder(root, Path.Combine(root, "skill")));
            Assert.IsFalse(SkillMarketplaceWindowViewModel.IsManagedFolder(root, Path.GetTempPath()));
            Assert.IsFalse(SkillMarketplaceWindowViewModel.IsManagedFolder(root, root + "-sibling"));
            Assert.IsFalse(SkillMarketplaceWindowViewModel.IsManagedFolder(root, Path.Combine(root, "missing")));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    [DataRow("en-US", false, 760, 560)]
    [DataRow("ko-KR", true, 760, 560)]
    [DataRow("en-US", true, 1000, 700)]
    [DataRow("ko-KR", false, 1000, 700)]
    public async Task MarketplaceRendersBothTabsAcrossCulturesThemesAndSizes(string cultureName, bool dark, int width, int height)
    {
        await HeadlessTestSession.Shared.Dispatch<bool>(async () =>
        {
            using var culture = new UiCultureScope(cultureName);
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var manager = new SkillManager();
            using var model = new SkillMarketplaceWindowViewModel(manager, new Marketplace(manager), new Messages());
            await model.LoadAsync();
            model.SelectedCatalogSkill = model.CatalogSkills.Single(x => x.Id == "pending");
            model.SelectedInstalledSkill = model.InstalledSkills.Single();
            var window = new SkillMarketplaceWindow { DataContext = model, Width = width, Height = height };
            try
            {
                window.Show();
                await Task.Yield();
                Dispatcher.UIThread.RunJobs();
                Assert.AreEqual(cultureName == "ko-KR" ? "스킬 마켓플레이스" : "Skill marketplace", window.Title);
                Assert.IsFalse(window.FindControl<Button>("CatalogInstall")!.IsEffectivelyEnabled);
                Assert.IsTrue(window.FindControl<Button>("CatalogOpenSource")!.IsEffectivelyEnabled);
                Assert.Contains(cultureName == "ko-KR" ? "라이선스" : "license", window.FindControl<TextBlock>("CatalogInstallReason")!.Text!);
                Save(window, $"marketplace-{cultureName}-{width}-{(dark ? "dark" : "light")}-catalog.png");
                foreach (var name in new[] { "CatalogInstall", "CatalogOpenSource", "CatalogOpenLicense" })
                {
                    var button = window.FindControl<Button>(name)!;
                    var position = button.TranslatePoint(new Point(0, 0), window);
                    Assert.IsNotNull(position);
                    Assert.IsLessThan(window.Bounds.Height - 45, position.Value.Y + button.Bounds.Height, $"{name} is outside the catalog viewport.");
                }
                window.FindControl<TabControl>("MarketplaceTabs")!.SelectedIndex = 1;
                await Task.Yield();
                Assert.IsTrue(window.FindControl<Button>("InstalledToggle")!.IsEffectivelyEnabled);
                Assert.IsTrue(window.FindControl<Button>("InstalledRemove")!.IsEffectivelyEnabled);
                Assert.Contains(cultureName == "ko-KR" ? "활성 스킬" : "enabled", window.FindControl<TextBlock>("InstalledSummary")!.Text!);
                Save(window, $"marketplace-{cultureName}-{width}-{(dark ? "dark" : "light")}-installed.png");
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public void UnsupportedUiCultureUsesEnglishWithoutChangingFormattingCulture()
    {
        using var culture = new UiCultureScope("ja-JP");
        var formatting = CultureInfo.CurrentCulture;
        Assert.AreEqual("Skill marketplace", ManagedAiText.MarketplaceTitle);
        Assert.AreSame(formatting, CultureInfo.CurrentCulture);
    }

    private static void Save(Window window, string filename)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts");
        Directory.CreateDirectory(directory);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame();
        Assert.IsNotNull(frame);
        frame.Save(Path.Combine(directory, filename));
    }

    private static SkillCatalogEntry Entry(string id) => new(id, "Finance", id == "pending" ? "전자공시 조회 / Disclosure search" : id,
        "기업의 공시와 재무 정보를 조회합니다. Search corporate disclosures and financial information.",
        "https://github.com/NomaDamas/k-skill", "k-dart", "", "NomaDamas", "MIT", "pending", "upstream", "pending", "listed", "required",
        "사용자 API 키를 사용합니다. User API credentials are required. The catalog lists candidates while installation review is pending.");

    private sealed class UiCultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentUICulture;
        public UiCultureScope(string name) => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
        public void Dispose() => CultureInfo.CurrentUICulture = _previous;
    }

    private sealed class SkillManager : IManagedAiSkillManager
    {
        public string StorageDirectory => "C:\\fixture-skills";
        public IReadOnlyList<AiSkill> Items = [new("fixture", "업무 절차 / Work instructions", "로컬 문서 작성 지원 / Local document support", "C:\\fixture-skills\\fixture", true)];
        public TaskCompletionSource? ToggleCompletion;
        public TaskCompletionSource ToggleStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<AiSkill>> ListAsync(CancellationToken token) => Task.FromResult(Items);
        public Task<IReadOnlyList<AiSkill>> ImportAsync(string sourceDirectory, CancellationToken token)
        {
            Items = Items.Append(new("imported", "Imported", "Imported description", "C:\\fixture-skills\\imported", true)).ToArray();
            return Task.FromResult(Items);
        }
        public async Task<IReadOnlyList<AiSkill>> SetEnabledAsync(string id, bool enabled, CancellationToken token)
        {
            ToggleStarted.TrySetResult();
            if (ToggleCompletion is not null) await ToggleCompletion.Task.WaitAsync(token);
            Items = Items.Select(x => x.Id == id ? x with { Enabled = enabled } : x).ToArray();
            return Items;
        }
        public Task<IReadOnlyList<AiSkill>> RemoveAsync(string id, CancellationToken token)
        {
            Items = Items.Where(x => x.Id != id).ToArray();
            return Task.FromResult(Items);
        }
    }

    private sealed class Marketplace(SkillManager manager) : IManagedAiSkillMarketplace
    {
        public bool Failure;
        public bool WaitForCancellation;
        public int InstallCount;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<IReadOnlyList<SkillCatalogEntry>> ListAsync(CancellationToken token)
        {
            Started.TrySetResult();
            if (WaitForCancellation) await Task.Delay(Timeout.Infinite, token);
            if (Failure) throw new ManagedAiException(AiFailureCode.CatalogUnavailable);
            return [Entry("FIXTURE") with { Author = "Local author" }, Entry("pending") with { Category = "Legal" },
                Entry("approved") with { SourceRef = new string('a', 40), LicenseReviewStatus = "verified", InstallReviewStatus = "approved",
                    LicenseSource = "https://github.com/NomaDamas/k-skill/blob/" + new string('a', 40) + "/LICENSE" }];
        }
        public Task<IReadOnlyList<AiSkill>> InstallAsync(string id, CancellationToken token)
        {
            InstallCount++;
            manager.Items = manager.Items.Append(new(id, id, "Installed from catalog", "C:\\fixture-skills\\" + id, true)).ToArray();
            return Task.FromResult(manager.Items);
        }
    }

    private sealed class Messages : IAppMessageBox
    {
        public int Questions;
        public AppMessageBoxResult Answer = AppMessageBoxResult.Yes;
        public AppMessageBoxResult DisplayQuestion(string message, AppMessageBoxButton buttons, AppMessageBoxResult answer) { Questions++; return Answer; }
        public AppMessageBoxResult DisplayInfo(string message, AppMessageBoxButton buttons) => throw new NotSupportedException();
        public AppMessageBoxResult DisplayError(Exception? reason, bool critical, string file, string member, int line) => throw new NotSupportedException();
        public AppMessageBoxResult DisplayError(string? message, bool critical, string file, string member, int line) => throw new NotSupportedException();
    }
}
