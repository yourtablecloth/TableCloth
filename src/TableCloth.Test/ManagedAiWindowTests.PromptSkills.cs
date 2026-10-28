using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using System.Globalization;
using TableCloth.ManagedAi;

namespace TableCloth.Test;

public sealed partial class ManagedAiWindowTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PromptSkillPickerFitsMinimumWindowInBothThemes(bool dark)
    {
        var screenshot = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts", $"prompt-skills-{(dark ? "dark" : "light")}.png");
        await WithPromptSkills(async (window, fixture) =>
        {
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            window.Width = 640;
            window.Height = 540;
            var input = Find<TextBox>(window, "ManagedAiChatInput");
            input.Focus();
            window.KeyTextInput("@");
            Dispatcher.UIThread.RunJobs();
            VisibleBounds(window, input);
            VisibleBounds(window, Find<Button>(window, "ManagedAiPromptAddSkill"));
            VisibleBounds(window, Find<ComboBox>(window, "ManagedAiPromptSkills"));
            VisibleBounds(window, Find<TextBlock>(window, "ManagedAiPromptSkillHint"));
            SaveFrame(window, screenshot);
            Assert.HasCount(0, fixture.Chat.Requests);
            await Task.CompletedTask;
        });
        TestContext.AddResultFile(screenshot);
    }

    [TestMethod]
    public Task PromptSkillButtonInsertsNativeSkillNameAtCaretWithoutSending()
        => WithPromptSkills(async (window, fixture) =>
        {
            var input = Find<TextBox>(window, "ManagedAiChatInput");
            input.Text = "Please  now";
            input.CaretIndex = 7;
            Click(window, Find<Button>(window, "ManagedAiPromptAddSkill"));
            var picker = Find<ComboBox>(window, "ManagedAiPromptSkills");
            Assert.HasCount(2, picker.Items);
            picker.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            Assert.AreEqual("Please $alpha-skill now", input.Text);
            Assert.IsTrue(input.IsFocused);
            Assert.HasCount(0, fixture.Chat.Requests);
            await Task.CompletedTask;
        });

    [TestMethod]
    public Task PromptAtMentionFiltersAndReplacesOnlyMentionAtCaret()
        => WithPromptSkills(async (window, fixture) =>
        {
            var input = Find<TextBox>(window, "ManagedAiChatInput");
            input.Text = "Use  please";
            input.CaretIndex = 4;
            input.Focus();
            window.KeyTextInput("@beta");
            Dispatcher.UIThread.RunJobs();
            var picker = Find<ComboBox>(window, "ManagedAiPromptSkills");
            Assert.IsTrue(picker.IsEffectivelyVisible);
            Assert.IsTrue(input.IsFocused);
            Assert.HasCount(1, picker.Items);
            picker.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            Assert.AreEqual("Use $beta-skill please", input.Text);
            Assert.HasCount(0, fixture.Chat.Requests);
            await Task.CompletedTask;
        });

    [TestMethod]
    public Task PromptEmailDoesNotOpenSkillPicker()
        => WithPromptSkills(async (window, fixture) =>
        {
            var input = Find<TextBox>(window, "ManagedAiChatInput");
            input.Focus();
            window.KeyTextInput("contact@example.com");
            Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(Find<ComboBox>(window, "ManagedAiPromptSkills").IsEffectivelyVisible);
            Assert.AreEqual("contact@example.com", input.Text);
            Assert.HasCount(0, fixture.Chat.Requests);
            await Task.CompletedTask;
        });

    [TestMethod]
    public Task PromptEscapeDismissesSkillPickerAndPreservesDraft()
        => WithPromptSkills(async (window, fixture) =>
        {
            var input = Find<TextBox>(window, "ManagedAiChatInput");
            input.Focus();
            window.KeyTextInput("Use @");
            Dispatcher.UIThread.RunJobs();
            Assert.IsTrue(Find<ComboBox>(window, "ManagedAiPromptSkills").IsEffectivelyVisible);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.IsFalse(Find<ComboBox>(window, "ManagedAiPromptSkills").IsEffectivelyVisible);
            Assert.AreEqual("Use @", input.Text);
            Assert.HasCount(0, fixture.Chat.Requests);
            await Task.CompletedTask;
        });

    [TestMethod]
    public Task PromptSkillPickerSupportsKeyboardWithoutSending()
        => WithPromptSkills(async (window, fixture) =>
        {
            var input = Find<TextBox>(window, "ManagedAiChatInput");
            input.Focus();
            window.KeyTextInput("@");
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.AreEqual("$alpha-skill ", input.Text);
            Assert.IsTrue(input.IsFocused);
            Assert.HasCount(0, fixture.Chat.Requests);
            await Task.CompletedTask;
        });

    [TestMethod]
    public Task PromptEscapeFromSkillListPreservesUncommittedMention()
        => WithPromptSkills(async (window, fixture) =>
        {
            var input = Find<TextBox>(window, "ManagedAiChatInput");
            input.Focus();
            window.KeyTextInput("@");
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.AreEqual("@", input.Text);
            Assert.IsFalse(Find<ComboBox>(window, "ManagedAiPromptSkills").IsEffectivelyVisible);
            Assert.IsTrue(input.IsFocused);
            Assert.HasCount(0, fixture.Chat.Requests);
            await Task.CompletedTask;
        });

    [TestMethod]
    public Task PromptSkillPickerClosesWhileSendingAndReenablesAfterResponse()
        => WithPromptSkills(async (window, fixture) =>
        {
            var input = Find<TextBox>(window, "ManagedAiChatInput");
            input.Text = "A question";
            Click(window, Find<Button>(window, "ManagedAiPromptAddSkill"));
            // Initiate the command directly: native ComboBox popups consume an
            // outside mouse click to dismiss before a second click activates Send.
            Find<Button>(window, "ManagedAiChatSend").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Until(() => fixture.Chat.Requests.Count == 1);
            Assert.IsFalse(Find<Button>(window, "ManagedAiPromptAddSkill").IsEnabled);
            Assert.IsFalse(Find<ComboBox>(window, "ManagedAiPromptSkills").IsEffectivelyVisible);
            fixture.Chat.Complete("A response");
            await Ready(window);
            Assert.IsTrue(Find<Button>(window, "ManagedAiPromptAddSkill").IsEnabled);
        });

    [TestMethod]
    public Task PromptSkillPickerReportsEmptyEnabledListWithoutChangingDraft()
        => WithPromptSkills(async (window, fixture) =>
        {
            var input = Find<TextBox>(window, "ManagedAiChatInput");
            input.Text = "Keep this draft";
            Click(window, Find<Button>(window, "ManagedAiPromptAddSkill"));
            Assert.HasCount(0, Find<ComboBox>(window, "ManagedAiPromptSkills").Items);
            Assert.IsFalse(string.IsNullOrWhiteSpace(Find<TextBlock>(window, "ManagedAiPromptSkillHint").Text));
            Assert.AreEqual("Keep this draft", input.Text);
            Assert.HasCount(0, fixture.Chat.Requests);
            await Task.CompletedTask;
        }, empty: true);

    [TestMethod]
    public Task PromptSkillPickerExcludesAmbiguousNativeNames()
        => WithPromptSkills(async (window, fixture) =>
        {
            Click(window, Find<Button>(window, "ManagedAiPromptAddSkill"));
            var picker = Find<ComboBox>(window, "ManagedAiPromptSkills");
            Assert.HasCount(1, picker.Items);
            Assert.AreEqual("beta-skill", ((AiSkill)picker.Items[0]!).Name);
            Assert.Contains("duplicate names", Find<TextBlock>(window, "ManagedAiPromptSkillHint").Text!);
            Assert.HasCount(0, fixture.Chat.Requests);
            await Task.CompletedTask;
        }, configure: fixture => fixture.Skills.Items = fixture.Skills.Items.Append(
            new AiSkill("second-alpha", "ALPHA-SKILL", "Ambiguous name", "fixture-second-alpha", true)).ToArray());

    [TestMethod]
    public Task PromptSkillInsertionPreservesDraftWhenMessageLimitWouldBeExceeded()
        => WithPromptSkills(async (window, fixture) =>
        {
            var input = Find<TextBox>(window, "ManagedAiChatInput");
            var draft = new string('a', input.MaxLength);
            input.Text = draft;
            input.CaretIndex = draft.Length;
            Click(window, Find<Button>(window, "ManagedAiPromptAddSkill"));
            Find<ComboBox>(window, "ManagedAiPromptSkills").SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            Assert.AreEqual(draft, input.Text);
            Assert.Contains("message limit", Find<TextBlock>(window, "ManagedAiStatus").Text!);
            Assert.HasCount(0, fixture.Chat.Requests);
            await Task.CompletedTask;
        });

    [TestMethod]
    public Task PromptShiftEnterDoesNotSendDuringImeComposition()
        => WithPromptSkills(async (window, fixture) =>
        {
            var input = Find<TextBox>(window, "ManagedAiChatInput");
            input.Text = "A question";
            input.Focus();
            var request = new TextInputMethodClientRequestedEventArgs
            {
                RoutedEvent = InputElement.TextInputMethodClientRequestedEvent
            };
            input.RaiseEvent(request);
            Assert.IsNotNull(request.Client);
            request.Client.SetPreeditText("한");
            try
            {
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Shift);
                window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.Shift);
                Dispatcher.UIThread.RunJobs();
                Assert.HasCount(0, fixture.Chat.Requests);
            }
            finally { request.Client.SetPreeditText(null); }
            await Task.CompletedTask;
        });

    private static Task WithPromptSkills(Func<ManagedAiWindow, Fixture, Task> action, bool empty = false,
        Action<Fixture>? configure = null)
        => HeadlessTestSession.Shared.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture();
            fixture.Skills.Items = empty ? [] :
            [
                new("directory-alpha", "alpha-skill", "Alpha description", "fixture-alpha", true),
                new("directory-beta", "beta-skill", "Beta description", "fixture-beta", true),
                new("disabled", "disabled-skill", "Disabled", "fixture-disabled", false),
                new("invalid", "invalid-skill", "Invalid", "fixture-invalid", true) { IsValid = false },
                new("dotted", "alpha.skill", "Not a single native mention", "fixture-dotted", true),
                new("reserved", "HOME", "Reserved variable", "fixture-reserved", true)
            ];
            configure?.Invoke(fixture);
            var window = fixture.Create(CultureInfo.GetCultureInfo("en-US"));
            try
            {
                window.Show();
                await Ready(window);
                await action(window, fixture);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
}
