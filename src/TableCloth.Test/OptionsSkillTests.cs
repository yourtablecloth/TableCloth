using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Globalization;
using TableCloth.Components;
using TableCloth.Dialogs;
using TableCloth.ManagedAi;
using TableCloth.Models;
using TableCloth.ViewModels;

namespace TableCloth.Test;

[TestClass, DoNotParallelize]
public sealed class OptionsSkillTests
{
    [TestMethod]
    public async Task SettingsCommandsListToggleAndRemoveTheSelectedSkill()
    {
        using var culture = new UiCultureScope("ko-KR");
        var skills = new SkillManager();
        var messages = new Messages();
        var viewModel = new OptionsWindowViewModel(null!, null!, messages, null!, new TaskFactory(), skills);

        await viewModel.RefreshSkillListCommand.ExecuteAsync(null);
        Assert.HasCount(1, viewModel.AiSkills);
        Assert.Contains("활성 스킬 1개", viewModel.SkillSummary);

        viewModel.SelectedAiSkill = viewModel.AiSkills.Single();
        await viewModel.ToggleAiSkillCommand.ExecuteAsync(null);
        Assert.IsFalse(skills.Items.Single().Enabled);
        Assert.AreEqual("활성화", viewModel.SkillToggleLabel);
        Assert.Contains("활성 스킬 0개", viewModel.SkillSummary);

        await viewModel.RemoveAiSkillCommand.ExecuteAsync(null);
        Assert.IsEmpty(skills.Items);
        Assert.IsEmpty(viewModel.AiSkills);
        Assert.AreEqual(1, messages.Questions);
    }

    [TestMethod]
    public async Task SettingsWindowDisplaysTheDedicatedSkillTabAndCatalog()
    {
        using var headless = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
        await headless.Dispatch<bool>(async () =>
        {
            using var culture = new UiCultureScope("ko-KR");
            Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
            var viewModel = new DesignViewModel { InitialTabIndex = 7, SkillSummary = "보유 스킬 1개, 활성 스킬 1개" };
            viewModel.AiSkills.Add(new(new("fixture-skill", "업무 절차", "업무 안내", "C:\\fixture-skill", true)));
            var window = new OptionsWindow { DataContext = viewModel, Width = 600, Height = 480 };
            try
            {
                window.Show();
                await Task.Yield();
                var tabs = window.GetVisualDescendants().OfType<TabControl>().Single();
                Assert.AreEqual(7, tabs.SelectedIndex);
                Assert.AreEqual("AI 스킬", ((TabItem)tabs.SelectedItem!).Header);
                Assert.HasCount(1, window.FindControl<ListBox>("AiSkillList")!.ItemsSource!.Cast<object>());
                Assert.Contains("활성 스킬 1개", window.FindControl<TextBlock>("AiSkillSummary")!.Text!);
                var screenshot = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts", "options-ai-skills-600-light.png");
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

    private sealed class UiCultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentUICulture;

        public UiCultureScope(string name) => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);

        public void Dispose() => CultureInfo.CurrentUICulture = _previous;
    }

    private sealed class DesignViewModel : OptionsWindowViewModel { }

    private sealed class SkillManager : IManagedAiSkillManager
    {
        public string StorageDirectory => "C:\\fixture-skills";
        public IReadOnlyList<AiSkill> Items = [new("fixture-skill", "업무 절차", "업무 안내", "C:\\fixture-skill", true)];
        public Task<IReadOnlyList<AiSkill>> ListAsync(CancellationToken token) => Task.FromResult(Items);
        public Task<IReadOnlyList<AiSkill>> ImportAsync(string sourceDirectory, CancellationToken token)
            => throw new AssertFailedException("Unexpected import");
        public Task<IReadOnlyList<AiSkill>> SetEnabledAsync(string id, bool enabled, CancellationToken token)
        {
            Items = Items.Select(x => x.Id == id ? x with { Enabled = enabled } : x).ToArray();
            return Task.FromResult(Items);
        }
        public Task<IReadOnlyList<AiSkill>> RemoveAsync(string id, CancellationToken token)
        {
            Items = Items.Where(x => x.Id != id).ToArray();
            return Task.FromResult(Items);
        }
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
