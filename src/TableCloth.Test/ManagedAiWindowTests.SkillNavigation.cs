using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Globalization;
using TableCloth.ManagedAi;

namespace TableCloth.Test;

public sealed partial class ManagedAiWindowTests
{
    [TestMethod]
    [DataRow(RawInputModifiers.None)]
    [DataRow(RawInputModifiers.Shift)]
    public Task PromptSkillDownDownEnterCommitsSecondCandidateWithoutSending(RawInputModifiers modifiers)
        => WithPromptSkills(async (window, fixture) =>
        {
            var input = OpenKeyboardSkillCandidates(window);
            var picker = Find<ComboBox>(window, "ManagedAiPromptSkills");
            Assert.IsTrue(input.IsFocused);
            PressSkillNavigationKey(window, PhysicalKey.ArrowDown);
            PressSkillNavigationKey(window, PhysicalKey.ArrowDown);
            AssertCurrentSkillCandidate(picker, 1);
            Assert.AreEqual(-1, picker.SelectedIndex);
            Assert.AreEqual("@", input.Text);
            PressSkillNavigationKey(window, PhysicalKey.Enter, modifiers);
            Assert.AreEqual("$beta-skill ", input.Text);
            Assert.IsTrue(input.IsFocused);
            Assert.HasCount(0, fixture.Chat.Requests);
            await Task.CompletedTask;
        });

    [TestMethod]
    public Task PromptSkillUpReturnsToFirstCandidateAndClampsAtBoundary()
        => WithPromptSkills(async (window, fixture) =>
        {
            var input = OpenKeyboardSkillCandidates(window);
            PressSkillNavigationKey(window, PhysicalKey.ArrowDown);
            PressSkillNavigationKey(window, PhysicalKey.ArrowDown);
            PressSkillNavigationKey(window, PhysicalKey.ArrowUp);
            PressSkillNavigationKey(window, PhysicalKey.ArrowUp);
            AssertCurrentSkillCandidate(Find<ComboBox>(window, "ManagedAiPromptSkills"), 0);
            Assert.AreEqual("@", input.Text);
            PressSkillNavigationKey(window, PhysicalKey.Enter);
            Assert.AreEqual("$alpha-skill ", input.Text);
            Assert.HasCount(0, fixture.Chat.Requests);
            await Task.CompletedTask;
        });

    [TestMethod]
    public Task PromptSkillUpFromInputStartsAtLastCandidateAndClampsDown()
        => WithPromptSkills(async (window, fixture) =>
        {
            var input = OpenKeyboardSkillCandidates(window);
            PressSkillNavigationKey(window, PhysicalKey.ArrowUp);
            PressSkillNavigationKey(window, PhysicalKey.ArrowDown);
            AssertCurrentSkillCandidate(Find<ComboBox>(window, "ManagedAiPromptSkills"), 1);
            PressSkillNavigationKey(window, PhysicalKey.Escape);
            Assert.AreEqual("@", input.Text);
            Assert.IsTrue(input.IsFocused);
            Assert.IsFalse(Find<ComboBox>(window, "ManagedAiPromptSkills").IsEffectivelyVisible);
            Assert.HasCount(0, fixture.Chat.Requests);
            await Task.CompletedTask;
        });

    [TestMethod]
    public Task PromptSkillPlusStartsAtFirstCandidateAndDownMovesToSecond()
        => WithPromptSkills(async (window, fixture) =>
        {
            var input = Find<TextBox>(window, "ManagedAiChatInput");
            input.Text = "Use "; input.CaretIndex = 4;
            Click(window, Find<Button>(window, "ManagedAiPromptAddSkill"));
            var picker = Find<ComboBox>(window, "ManagedAiPromptSkills");
            AssertCurrentSkillCandidate(picker, 0);
            PressSkillNavigationKey(window, PhysicalKey.ArrowDown);
            AssertCurrentSkillCandidate(picker, 1);
            PressSkillNavigationKey(window, PhysicalKey.Enter);
            Assert.AreEqual("Use $beta-skill ", input.Text);
            Assert.HasCount(0, fixture.Chat.Requests);
            await Task.CompletedTask;
        });

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PromptSkillCurrentCandidateUsesVisibleThemePaintDespiteOtherPointerRow(bool dark)
    {
        var screenshot = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts",
            $"prompt-skill-keyboard-current-{(dark ? "dark" : "light")}.png");
        await WithPromptSkills(async (window, fixture) =>
        {
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            OpenKeyboardSkillCandidates(window);
            var picker = Find<ComboBox>(window, "ManagedAiPromptSkills");
            PressSkillNavigationKey(window, PhysicalKey.ArrowDown);
            PressSkillNavigationKey(window, PhysicalKey.ArrowDown);
            var current = AssertCurrentSkillCandidate(picker, 1);
            var other = (ComboBoxItem)picker.ContainerFromIndex(0)!;
            var top = TopLevel.GetTopLevel(current)!;
            var pointer = other.TranslatePoint(new Point(other.Bounds.Width / 2, other.Bounds.Height / 2), top);
            Assert.IsNotNull(pointer);
            top.MouseMove(pointer.Value);
            Dispatcher.UIThread.RunJobs();
            var root = current.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "Root");
            var presenter = current.GetVisualDescendants().OfType<ContentPresenter>()
                .Single(content => content.Name == "PART_ContentPresenter");
            Assert.IsInstanceOfType<ISolidColorBrush>(root.Background);
            Assert.IsInstanceOfType<ISolidColorBrush>(presenter.Foreground);
            Assert.AreEqual(dark ? ThemeVariant.Dark : ThemeVariant.Light, current.ActualThemeVariant);
            // FindResource(key) uses a null theme, which selects this app's Default
            // (light) resource dictionary. DynamicResource uses the actual variant.
            var accent = (ISolidColorBrush)current.FindResource(current.ActualThemeVariant, "AccentFillColorDefaultBrush")!;
            var foreground = (ISolidColorBrush)current.FindResource(current.ActualThemeVariant, "TextOnAccentFillColorPrimaryBrush")!;
            Assert.AreEqual(accent.Color, ((ISolidColorBrush)root.Background!).Color);
            Assert.AreEqual(foreground.Color, ((ISolidColorBrush)presenter.Foreground!).Color);
            var textBlocks = current.GetVisualDescendants().OfType<TextBlock>().ToArray();
            Assert.IsGreaterThanOrEqualTo(2, textBlocks.Length);
            foreach (var textBlock in textBlocks)
            {
                Assert.IsInstanceOfType<ISolidColorBrush>(textBlock.Foreground);
                Assert.AreEqual(foreground.Color, ((ISolidColorBrush)textBlock.Foreground!).Color,
                    "The candidate's rendered name and description must use the foreground on the accent background.");
            }
            Assert.IsGreaterThan((byte)0, ((ISolidColorBrush)root.Background!).Color.A);
            AssertCurrentSkillCandidate(picker, 1);
            Directory.CreateDirectory(Path.GetDirectoryName(screenshot)!);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            using var frame = top.CaptureRenderedFrame();
            Assert.IsNotNull(frame); frame.Save(screenshot);
            Assert.HasCount(0, fixture.Chat.Requests);
            await Task.CompletedTask;
        });
        TestContext.AddResultFile(screenshot);
    }

    [TestMethod]
    public async Task PromptSkillNavigationScrollsToCandidatesBeyondTheVisibleRows()
    {
        await HeadlessTestSession.Shared.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture();
            fixture.Skills.Items = Enumerable.Range(0, 24)
                .Select(index => new AiSkill($"folder-{index}", $"skill-{index:00}", $"Description {index}", $"fixture-{index}", true))
                .ToArray();
            var window = fixture.Create(CultureInfo.GetCultureInfo("en-US"));
            try
            {
                window.Show(); await Ready(window);
                var input = OpenKeyboardSkillCandidates(window);
                for (var index = 0; index < 19; index++) PressSkillNavigationKey(window, PhysicalKey.ArrowDown);
                var picker = Find<ComboBox>(window, "ManagedAiPromptSkills");
                var current = AssertCurrentSkillCandidate(picker, 18);
                Assert.IsTrue(current.IsEffectivelyVisible);
                var scroll = current.GetVisualAncestors().OfType<ScrollViewer>().First();
                var position = current.TranslatePoint(default, scroll);
                Assert.IsNotNull(position);
                Assert.IsTrue(position.Value.Y >= -1 && position.Value.Y + current.Bounds.Height <= scroll.Bounds.Height + 1);
                Assert.AreEqual("@", input.Text);
                PressSkillNavigationKey(window, PhysicalKey.Enter);
                Assert.AreEqual("$skill-18 ", input.Text);
                Assert.HasCount(0, fixture.Chat.Requests);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None);
    }

    [TestMethod]
    public Task PromptSkillFilterInvalidatesQueuedCandidateFocus()
        => WithPromptSkills(async (window, fixture) =>
        {
            var input = OpenKeyboardSkillCandidates(window);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            input.Focus(); input.Text = "@beta"; input.CaretIndex = 5;
            Dispatcher.UIThread.RunJobs();
            var picker = Find<ComboBox>(window, "ManagedAiPromptSkills");
            Assert.IsTrue(input.IsFocused);
            Assert.HasCount(1, picker.Items);
            Assert.IsFalse(picker.GetRealizedContainers().Any(container => container.Classes.Contains("prompt-skill-current")));
            PressSkillNavigationKey(window, PhysicalKey.ArrowDown);
            AssertCurrentSkillCandidate(picker, 0);
            PressSkillNavigationKey(window, PhysicalKey.Enter);
            Assert.AreEqual("$beta-skill ", input.Text);
            Assert.HasCount(0, fixture.Chat.Requests);
            await Task.CompletedTask;
        });

    private static TextBox OpenKeyboardSkillCandidates(ManagedAiWindow window)
    {
        var input = Find<TextBox>(window, "ManagedAiChatInput");
        input.Focus(); window.KeyTextInput("@"); Dispatcher.UIThread.RunJobs();
        return input;
    }

    private static void PressSkillNavigationKey(ManagedAiWindow window, PhysicalKey key,
        RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static ComboBoxItem AssertCurrentSkillCandidate(ComboBox picker, int index)
    {
        Assert.IsInstanceOfType<ComboBoxItem>(picker.ContainerFromIndex(index));
        var current = (ComboBoxItem)picker.ContainerFromIndex(index)!;
        Assert.IsTrue(current.IsFocused, $"Candidate {index} does not have keyboard focus.");
        Assert.IsTrue(current.Classes.Contains("prompt-skill-current"));
        Assert.HasCount(1, picker.GetRealizedContainers().Where(container => container.Classes.Contains("prompt-skill-current")).ToArray());
        return current;
    }
}
