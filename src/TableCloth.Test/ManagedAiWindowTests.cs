using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TableCloth.Components;
using TableCloth.Dialogs;
using TableCloth.ViewModels;
using TableCloth.ManagedAi;
using TableCloth.Models;
using TableCloth.Models.Configuration;
using TableCloth.Serialization;
using TableCloth.ManagedAi.OpenAi;
using System.Text.Json;
using System.Globalization;
using TableCloth.Theme.Controls;
using TableCloth.Pages;

namespace TableCloth.Test;

[TestClass, DoNotParallelize]
public sealed partial class ManagedAiWindowTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task QuickStartOpensAiAsModalChildOfMainWindow()
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var ai = new Fixture().Create();
            var previous = TableClothApplication.ServiceProvider;
            TableClothApplication.ServiceProvider = new AiWindowProvider(ai);
            var page = new QuickStartPage();
            var owner = new Window { Content = page, Width = 800, Height = 600 };
            try
            {
                owner.Show();
                Dispatcher.UIThread.RunJobs();
                var dialogTask = page.OpenManagedAiAsync("식탁보 사용법을 알려 주세요.");
                await Until(() => ai.IsVisible);
                Assert.AreSame(owner, ai.Owner);
                Assert.AreEqual(840d, ai.Width);
                Assert.AreEqual(640d, ai.Height);
                Assert.IsTrue(ai.Width > owner.Width && ai.Height > owner.Height);
                Assert.IsFalse(dialogTask.IsCompleted);
                Assert.AreEqual("식탁보 사용법을 알려 주세요.", Find<TextBox>(ai, "ManagedAiChatInput").Text);
                await Ready(ai);
                ai.Close();
                await dialogTask;
            }
            finally
            {
                ai.Close();
                owner.Close();
                TableClothApplication.ServiceProvider = previous;
            }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task StartupChecksStayOffUiThread()
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture();
            var window = fixture.Create();
            try
            {
                Assert.AreEqual(WindowStartupLocation.CenterOwner, window.WindowStartupLocation);
                window.Show(); await Ready(window);
                Assert.IsFalse(fixture.Skills.ListCalledOnUiThread ?? true);
                Assert.IsFalse(fixture.Runtime.GetActiveCalledOnUiThread ?? true);
                Assert.IsFalse(fixture.Auth.StatusCalledOnUiThread ?? true);
                Assert.IsFalse(fixture.Models.ListCalledOnUiThread ?? true);
                Assert.IsFalse(fixture.Preferences.LoadCalledOnUiThread ?? true);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task EnglishUiUsesEnglishAcrossChatAndSkillSettings()
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture();
            var window = fixture.Create(CultureInfo.GetCultureInfo("en-US"));
            try
            {
                Assert.AreEqual("TableCloth AI", window.Title);
                window.Show(); await Ready(window);
                Assert.AreEqual("Send", Find<Button>(window, "ManagedAiChatSend").Content);
                Assert.Contains("enabled skills", Find<TextBlock>(window, "ManagedAiActiveSkillCount").Text!);
                Assert.AreEqual("Ready to chat. Press Shift+Enter to send a message.", Find<TextBlock>(window, "ManagedAiStatus").Text);
                Click(window, Find<Button>(window, "ManagedAiManage"));
                Assert.AreEqual("Sign out", Find<Button>(window, "ManagedAiSignOut").Content);
                Assert.AreEqual("Manage installed AI skills", Find<Button>(window, "ManagedAiSkillMarketplace").Content);
                Assert.AreEqual("AI skills", ManagedAiText.SkillsTabTitle);
                Click(window, Find<Button>(window, "ManagedAiSettingsClose"));
                Find<TextBox>(window, "ManagedAiChatInput").Text = "How do I use TableCloth?";
                Click(window, Find<Button>(window, "ManagedAiChatSend"));
                await Until(() => fixture.Chat.Requests.Count == 1);
                var context = fixture.Chat.Requests[0].ClientContext;
                Assert.IsNotNull(context);
                Assert.AreEqual(AiResponseLanguage.English, context.ResponseLanguage);
                Assert.AreEqual("https://yourtablecloth.app/", context.OfficialHomepage.AbsoluteUri);
                Assert.AreEqual(typeof(ManagedAiWindow).Assembly.GetName().Version, context.InstalledVersion);
                fixture.Chat.Complete("Open the Quick Start screen.");
                await Ready(window);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    [DataRow("en-US", false, 840, 640)]
    [DataRow("en-US", true, 840, 640)]
    [DataRow("ko-KR", false, 840, 640)]
    [DataRow("ko-KR", true, 840, 640)]
    [DataRow("en-US", false, 640, 640)]
    [DataRow("en-US", true, 640, 640)]
    [DataRow("ko-KR", false, 640, 640)]
    [DataRow("ko-KR", true, 640, 640)]
    [DataRow("en-US", false, 640, 540)]
    [DataRow("en-US", true, 640, 540)]
    [DataRow("ko-KR", false, 640, 540)]
    [DataRow("ko-KR", true, 640, 540)]
    public async Task CompactSummaryAndWelcomeFitWithinTheAiWindow(string culture, bool dark, int width, int height)
    {
        var screenshot = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts",
            $"chat-welcome-{culture}-{width}x{height}-{(dark ? "dark" : "light")}.png");
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var fixture = new Fixture();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            fixture.Models.Items = [new("gpt-6-astra", "Astra", true) { TokenCost = new(250, 25, 1250, today) },
                new("gpt-6-luna", "Luna") { TokenCost = new(2.5m, .25m, 12.5m, today) }];
            var window = fixture.Create(CultureInfo.GetCultureInfo(culture));
            window.Width = width;
            window.Height = height;
            try
            {
                Assert.AreEqual((double)height, window.Height);
                var caption = culture == "ko-KR" ? "식탁보 AI" : "TableCloth AI";
                Assert.AreEqual(caption, window.Title);
                window.Show(); await Ready(window);
                Dispatcher.UIThread.RunJobs();
                Assert.IsFalse(Controls(window).OfType<TextBlock>().Any(text =>
                    text.Text is "식탁보 AI" or "식탁보 AI (베타)" or "TableCloth AI" or "TableCloth AI (Beta)"));

                var summary = Find<Grid>(window, "ManagedAiSummaryRow");
                var runtime = Find<TextBlock>(window, "ManagedAiRuntimeStatus");
                var models = Find<ComboBox>(window, "ManagedAiModels");
                var cost = Find<TextBlock>(window, "ManagedAiModelHint");
                Assert.Contains("Codex 0.156.1", runtime.Text!);
                Assert.Contains("ChatGPT", runtime.Text!);
                Assert.AreEqual("gpt-6-luna", ((AiModel)models.SelectedItem!).Id);
                Assert.Contains("gpt-6-luna", AutomationProperties.GetName(models)!);
                Assert.Contains("gpt-6-luna", AutomationProperties.GetHelpText(models)!);
                Assert.Contains(2.5m.ToString("0.###", CultureInfo.CurrentCulture), cost.Text!);
                Assert.Contains(12.5m.ToString("0.###", CultureInfo.CurrentCulture), cost.Text!);
                Assert.DoesNotContain("\n", cost.Text!);

                var summaryBounds = VisibleBounds(window, summary);
                var runtimeBounds = VisibleBounds(window, runtime);
                var modelBounds = VisibleBounds(window, models);
                var costBounds = VisibleBounds(window, cost);
                foreach (var bounds in new[] { runtimeBounds, modelBounds, costBounds })
                {
                    Assert.IsTrue(bounds.Left >= summaryBounds.Left - 1 && bounds.Right <= summaryBounds.Right + 1);
                    Assert.IsTrue(bounds.Top >= summaryBounds.Top - 1 && bounds.Bottom <= summaryBounds.Bottom + 1);
                    Assert.IsLessThanOrEqualTo(2d, Math.Abs(bounds.Center.Y - modelBounds.Center.Y),
                        "Runtime status, model selection, and token costs must occupy one row.");
                }
                Assert.IsLessThanOrEqualTo(modelBounds.Left + 1, runtimeBounds.Right, "Runtime status overlaps model selection.");
                Assert.IsLessThanOrEqualTo(costBounds.Left + 1, modelBounds.Right, "Model selection overlaps token costs.");
                foreach (var control in new Control[] { Find<TextBox>(window, "ManagedAiChatInput"),
                    Find<Button>(window, "ManagedAiChatSend"), Find<Button>(window, "ManagedAiNewChat"),
                    Find<Button>(window, "ManagedAiManage") })
                    VisibleBounds(window, control);
                var notice = Find<Border>(window, "ManagedAiPrivacyNotice");
                Assert.IsTrue(notice.GetVisualAncestors().Contains(Find<StackPanel>(window, "ManagedAiStarters")));
                VisibleBounds(window, notice);
                var starters = Controls(window).OfType<Button>().Where(x => x.Classes.Contains("chat-starter")).ToArray();
                CollectionAssert.AreEqual(culture == "ko-KR"
                    ? new[] { "서비스 찾기", "공공서비스 찾기", "세금 신고 사이트 찾기" }
                    : new[] { "Find a service", "Find a public service", "Find a tax filing site" },
                    starters.Select(x => ((StackPanel)x.Content!).Children.OfType<TextBlock>().First().Text).ToArray());
                foreach (var starter in starters) VisibleBounds(window, starter);
                var browseSkills = Find<Button>(window, "ManagedAiBrowseSkills");
                Assert.IsTrue(browseSkills.Classes.Contains("link"));
                Assert.AreEqual(culture == "ko-KR" ? "더 많은 기능은 스킬 설정 창에서 새 스킬을 받아 활용할 수 있습니다."
                    : "Get new skills from skill settings to use more features.", AutomationProperties.GetName(browseSkills));
                VisibleBounds(window, browseSkills);
                SaveFrame(window, screenshot);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
        TestContext.AddResultFile(screenshot);
    }

    [TestMethod]
    public async Task InitialQuestionAppearsAsAnUnsentChatDraft()
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var window = new Fixture().Create();
            try
            {
                window.SetInitialPrompt("이 사이트는 어떻게 이용하나요?");
                window.Show(); await Ready(window);
                Assert.AreEqual("이 사이트는 어떻게 이용하나요?", Find<TextBox>(window, "ManagedAiChatInput").Text);
                Assert.IsTrue(Find<Border>(window, "ManagedAiPrivacyNotice").IsEffectivelyVisible);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task ModelChoiceSurvivesReopeningAndRetainsOtherPreferences()
    {
        var screenshot = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts", "chat-model-cost-640-light.png");
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
            var fixture = new Fixture();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            fixture.Models.Items = [new("gpt-6-astra", "Astra", true) { TokenCost = new(250, 25, 1250, today) },
                new("gpt-6-luna", "Luna") { TokenCost = new(2.5m, .25m, 12.5m, today) }];
            fixture.Preferences.Json = """{"UseAudioRedirection":true,"Favorites":["kept"],"ShareNpkiFolder":false}""";
            var window = fixture.Create();
            try
            {
                window.Width = 640;
                window.Show(); await Ready(window);
                var models = Find<ComboBox>(window, "ManagedAiModels");
                Assert.AreEqual("gpt-6-luna", ((AiModel)models.SelectedItem!).Id);
                Assert.Contains(2.5m.ToString("0.###", CultureInfo.CurrentCulture), Find<TextBlock>(window, "ManagedAiModelHint").Text!);
                Assert.Contains(12.5m.ToString("0.###", CultureInfo.CurrentCulture), Find<TextBlock>(window, "ManagedAiModelHint").Text!);
                var notice = Find<Border>(window, "ManagedAiPrivacyNotice");
                Assert.IsTrue(notice.IsEffectivelyVisible);
                Assert.Contains("민감 개인정보", AutomationProperties.GetName(notice)!);
                var noticePosition = notice.TranslatePoint(new Point(0, 0), window);
                Assert.IsNotNull(noticePosition);
                Assert.IsTrue(noticePosition.Value.Y >= 0 && noticePosition.Value.Y + notice.Bounds.Height <= window.Bounds.Height);
                SaveFrame(window, screenshot);
                // A separate settings edit after opening the window must not be overwritten.
                var settings = fixture.Preferences.Read(); settings.Favorites.Add("added-later");
                fixture.Preferences.Json = Preferences.Serialize(settings);
                models.SelectedIndex = 0;
                await Until(() => fixture.Preferences.Read().LastSelectedAiModel == "gpt-6-astra");
                Assert.Contains("250", Find<TextBlock>(window, "ManagedAiModelHint").Text!);
                Assert.Contains("1250", Find<TextBlock>(window, "ManagedAiModelHint").Text!);
                window.Close(); await Until(() => !window.IsVisible);
                window = fixture.Create(); window.Show(); await Ready(window);
                Assert.AreEqual("gpt-6-astra", ((AiModel)Find<ComboBox>(window, "ManagedAiModels").SelectedItem!).Id);
                settings = fixture.Preferences.Read();
                Assert.IsTrue(settings.UseAudioRedirection);
                Assert.IsFalse(settings.ShareNpkiFolder);
                CollectionAssert.AreEqual(new[] { "kept", "added-later" }, settings.Favorites);
                Click(window, Find<Button>(window, "ManagedAiManage"));
                Click(window, Find<Button>(window, "ManagedAiSignOut")); await Ready(window);
                Assert.AreEqual("gpt-6-astra", fixture.Preferences.Read().LastSelectedAiModel);
                Click(window, Find<Button>(window, "ManagedAiConnect")); await Ready(window);
                Assert.AreEqual("gpt-6-astra", ((AiModel)Find<ComboBox>(window, "ManagedAiModels").SelectedItem!).Id);
                window.Close(); await Until(() => !window.IsVisible);
                fixture.Models.Items = [fixture.Models.Items[1]];
                window = fixture.Create(); window.Show(); await Ready(window);
                Assert.AreEqual("gpt-6-luna", ((AiModel)Find<ComboBox>(window, "ManagedAiModels").SelectedItem!).Id);
                Assert.AreEqual("gpt-6-luna", fixture.Preferences.Read().LastSelectedAiModel);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
        TestContext.AddResultFile(screenshot);
    }

    [TestMethod]
    public async Task ClosingDrainsRapidSelectionsInOrder()
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture(); var window = fixture.Create();
            try
            {
                window.Show(); await Ready(window);
                fixture.Preferences.SaveGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
                var models = Find<ComboBox>(window, "ManagedAiModels");
                models.SelectedIndex = 1; models.SelectedIndex = 0; models.SelectedIndex = 1;
                window.Close(); Assert.IsTrue(window.IsVisible);
                fixture.Preferences.SaveGate.SetResult();
                await Until(() => !window.IsVisible);
                Assert.AreEqual("fixture-other", fixture.Preferences.Read().LastSelectedAiModel);
                Assert.AreEqual(1, fixture.Preferences.MaxConcurrentSaves);
                CollectionAssert.AreEqual(new[] { "fixture-default", "fixture-other", "fixture-default", "fixture-other" }, fixture.Preferences.Saved);
            }
            finally { fixture.Preferences.SaveGate?.TrySetResult(); window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task SaveFailureIsVisibleWithoutDiscardingCurrentChoiceAndCanRecover()
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture(); var window = fixture.Create();
            try
            {
                window.Show(); await Ready(window);
                fixture.Preferences.FailSave = true;
                var models = Find<ComboBox>(window, "ManagedAiModels"); models.SelectedIndex = 1;
                await Until(() => Find<TextBlock>(window, "ManagedAiPreferenceNotice").IsVisible);
                Assert.AreEqual("fixture-other", ((AiModel)models.SelectedItem!).Id);
                Assert.AreEqual("fixture-default", fixture.Preferences.Read().LastSelectedAiModel);
                Find<TextBox>(window, "ManagedAiChatInput").Text = "전송하지 않는 테스트";
                await Until(() => Find<Button>(window, "ManagedAiChatSend").IsEnabled);
                Assert.IsTrue(Find<Button>(window, "ManagedAiChatSend").IsEnabled);
                fixture.Preferences.FailSave = false;
                models.SelectedIndex = 0; models.SelectedIndex = 1;
                await Until(() => fixture.Preferences.Read().LastSelectedAiModel == "fixture-other");
                await Until(() => !Find<TextBlock>(window, "ManagedAiPreferenceNotice").IsVisible);
                Assert.HasCount(0, fixture.Chat.Requests);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    [DataRow(640, false)]
    [DataRow(900, true)]
    public async Task ModelSelectionKeyboardCopyAndVisibleProgressWorkTogether(int width, bool dark)
    {
        var screenshot = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts", $"chat-{width}-{(dark ? "dark" : "light")}.png");
        var headless = HeadlessTestSession.Shared;
        // Explicit Task<bool> dispatch awaits the whole async test; Dispatch<Task> would return a nested task.
        await headless.Dispatch<bool>(async () =>
        {
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var fixture = new Fixture();
            var window = fixture.Create();
            window.Width = width;
            try
            {
                Assert.AreEqual("식탁보 AI", window.Title);
                window.Show();
                await Ready(window);
                Assert.IsFalse(Find<Button>(window, "ManagedAiConnect").IsVisible);
                var models = Find<ComboBox>(window, "ManagedAiModels");
                Assert.AreEqual("fixture-default", ((AiModel)models.SelectedItem!).Id);
                VisibleBounds(window, models);
                Assert.IsTrue(models.Focus(NavigationMethod.Tab));
                Dispatcher.UIThread.RunJobs();
                Assert.IsTrue(ToolTip.GetIsOpen(models));
                Assert.Contains("fixture-default", ToolTip.GetTip(models)!.ToString()!);
                window.KeyPressQwerty(PhysicalKey.F4, RawInputModifiers.None);
                window.KeyReleaseQwerty(PhysicalKey.F4, RawInputModifiers.None);
                await Until(() => models.IsDropDownOpen);
                Assert.IsFalse(ToolTip.GetIsOpen(models));
                window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                await Until(() => !models.IsDropDownOpen);
                Assert.IsTrue(models.Focus(NavigationMethod.Tab));
                window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
                window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
                await Until(() => (models.SelectedItem as AiModel)?.Id == "fixture-other");
                Assert.Contains("fixture-other", AutomationProperties.GetName(models)!);
                Assert.Contains("다른 모델", AutomationProperties.GetName(models)!);
                Assert.Contains("fixture-other", AutomationProperties.GetHelpText(models)!);
                Assert.IsTrue(ToolTip.GetIsOpen(models));
                Assert.Contains("fixture-other", ToolTip.GetTip(models)!.ToString()!);
                var input = Find<TextBox>(window, "ManagedAiChatInput");
                input.Focus(); input.Text = "첫 줄"; input.CaretIndex = input.Text.Length;
                Assert.IsFalse(ToolTip.GetIsOpen(models));
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
                Assert.AreEqual("첫 줄\n", input.Text!.Replace("\r\n", "\n"));
                Assert.HasCount(0, fixture.Chat.Requests);
                input.Text += "둘째 줄";
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Shift);
                window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.Shift);
                await Until(() => fixture.Chat.Requests.Count == 1);
                Assert.AreEqual("fixture-other", fixture.Chat.Requests.Single().Model);
                Assert.AreEqual("첫 줄\n둘째 줄", fixture.Chat.Requests.Single().Message.Replace("\r\n", "\n"));
                var pending = Find<Border>(window, "ManagedAiPendingResponse");
                Assert.IsTrue(pending.IsVisible);
                Assert.IsTrue(pending.GetVisualDescendants().OfType<ProgressRing>().Single().IsIndeterminate);
                Assert.IsFalse(models.IsEnabled);
                Assert.IsFalse(Find<Button>(window, "ManagedAiChatSend").IsEnabled);
                fixture.Chat.Progress!.Report(new("Searching", 2));
                await Until(() => pending.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Text?.Contains("2회") == true));
                Directory.CreateDirectory(Path.GetDirectoryName(screenshot)!);
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                await Task.Delay(300);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using (var frame = window.CaptureRenderedFrame()) { Assert.IsNotNull(frame); frame.Save(screenshot); }
                // A busy turn cannot send again, even if the key event arrives directly.
                window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Shift);
                Assert.HasCount(1, fixture.Chat.Requests);
                const string markdown = "## 응답\n\n**강조**와 [웹사이트](https://example.com/)\n\n```text\n원문\n```";
                fixture.Chat.Complete(markdown);
                await Until(() => !Find<Button>(window, "ManagedAiCancel").IsVisible);
                Assert.IsFalse(Controls(window).Any(x => AutomationProperties.GetAutomationId(x) == "ManagedAiPendingResponse"));
                Assert.IsTrue(Controls(window).OfType<TextBlock>().Any(x => x.Text == "식탁보 / fixture-other"));
                var copies = Controls(window).OfType<Button>().Where(x => x.Classes.Contains("chat-copy")).ToArray();
                Assert.HasCount(2, copies); // User and assistant.
                copies[^1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Until(() => copies[^1].Content as string == "복사 완료");
                Assert.AreEqual(markdown, await window.Clipboard!.TryGetTextAsync());
                models.SelectedIndex = 0;
                input.Text = "후속 질문";
                Find<Button>(window, "ManagedAiChatSend").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Until(() => fixture.Chat.Requests.Count == 2);
                Assert.AreEqual("fixture-default", fixture.Chat.Requests[1].Model);
                Assert.HasCount(2, fixture.Chat.Requests[1].History);
                Find<Button>(window, "ManagedAiCancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Until(() => !Find<Button>(window, "ManagedAiCancel").IsVisible);
                Assert.IsTrue(models.IsEnabled);
                Assert.IsFalse(input.IsReadOnly);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
        TestContext.AddResultFile(screenshot);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SingleConnectionActionInstallsIfNeededLogsInAndLoadsModels(bool installed)
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture(); fixture.Runtime.Installed = installed; fixture.Auth.LoggedIn = false;
            var window = fixture.Create();
            try
            {
                window.Show(); await Ready(window);
                var connect = Find<Button>(window, "ManagedAiConnect");
                Assert.AreEqual(installed ? "OpenAI 로그인" : "AI 사용 준비", connect.Content);
                Find<TextBox>(window, "ManagedAiChatInput").Text = "작성하던 메시지";
                connect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Ready(window);
                Assert.IsFalse(connect.IsVisible);
                Assert.AreEqual(installed ? 0 : 1, fixture.Runtime.Installs);
                Assert.AreEqual(installed ? 0 : 1, fixture.Messages.Questions);
                Assert.AreEqual(AiLoginMethod.Browser, fixture.Auth.Method);
                Assert.AreEqual(1, fixture.Models.Calls);
                Assert.IsTrue(Find<Button>(window, "ManagedAiChatSend").IsEnabled);
                Assert.AreEqual("작성하던 메시지", Find<TextBox>(window, "ManagedAiChatInput").Text);
                Assert.HasCount(0, fixture.Chat.Requests);
                Click(window, Find<Button>(window, "ManagedAiManage"));
                Click(window, Find<Button>(window, "ManagedAiSignOut"));
                await Until(() => connect.IsVisible && connect.IsEnabled);
                Assert.AreEqual("OpenAI 로그인", connect.Content);
                Assert.IsNull(Find<ComboBox>(window, "ManagedAiModels").SelectedItem);
                Assert.IsFalse(Find<Button>(window, "ManagedAiChatSend").IsEnabled);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task CanceledLoginAfterInstallationRetainsInstalledRuntimeStatus()
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture();
            fixture.Runtime.Installed = false;
            fixture.Auth.LoggedIn = false;
            fixture.Auth.LoginCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var window = fixture.Create(CultureInfo.GetCultureInfo("en-US"));
            try
            {
                window.Show(); await Ready(window);
                var runtime = Find<TextBlock>(window, "ManagedAiRuntimeStatus");
                Assert.Contains("Not installed", runtime.Text!);
                Click(window, Find<Button>(window, "ManagedAiConnect"));
                await Until(() => fixture.Auth.Progress is not null);
                Assert.IsTrue(fixture.Runtime.Installed);
                Assert.AreEqual(1, fixture.Runtime.Installs);
                Assert.IsFalse(fixture.Auth.LoginCompletion.Task.IsCompleted);
                Click(window, Find<Button>(window, "ManagedAiCancel"));
                await Ready(window);

                Assert.Contains("Codex 0.156.1", runtime.Text!);
                Assert.Contains("ChatGPT offline", runtime.Text!);
                Assert.DoesNotContain("Not installed", runtime.Text!);
                Assert.IsTrue(fixture.Runtime.Installed);
                Assert.IsFalse(fixture.Auth.LoggedIn);
                Assert.AreEqual(0, fixture.Models.Calls);
                Assert.IsNull(Find<ComboBox>(window, "ManagedAiModels").SelectedItem);
                Assert.HasCount(0, fixture.Chat.Requests);
                Assert.AreEqual("Sign in to OpenAI", Find<Button>(window, "ManagedAiConnect").Content);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    [DataRow(640, false)]
    [DataRow(900, true)]
    public async Task SettingsPanelRendersAndSupportsLogoutLoginAndReconnect(int width, bool dark)
    {
        var screenshot = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts", $"chat-settings-{width}-{(dark ? "dark" : "light")}.png");
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var fixture = new Fixture();
            var window = fixture.Create(); window.Width = width;
            try
            {
                window.Show(); await Ready(window);
                var manage = Find<Button>(window, "ManagedAiManage");
                Assert.AreEqual("OpenAI 계정 / 설정", manage.Content);
                Click(window, manage);
                var overlay = Find<Grid>(window, "ManagedAiSettings");
                Assert.IsTrue(overlay.IsVisible);
                var logout = Find<Button>(window, "ManagedAiSignOut");
                Assert.IsTrue(logout.IsEffectivelyVisible);
                Assert.IsGreaterThan(100d, logout.Bounds.Width);
                Assert.IsTrue(logout.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Text == "로그아웃"));
                SaveFrame(window, screenshot);
                window.Height = 540;
                Dispatcher.UIThread.RunJobs();
                var panel = Find<Border>(window, "ManagedAiSettingsPanel");
                var panelEnd = panel.TranslatePoint(new Point(panel.Bounds.Width, panel.Bounds.Height), window)!.Value;
                Assert.IsLessThanOrEqualTo(window.ClientSize.Height, panelEnd.Y);
                window.Height = 780;
                Dispatcher.UIThread.RunJobs();
                window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                window.KeyReleaseQwerty(PhysicalKey.Escape, RawInputModifiers.None);
                Assert.IsFalse(overlay.IsVisible);
                Click(window, manage);
                Click(window, Find<Button>(window, "ManagedAiSettingsClose"));
                Assert.IsFalse(overlay.IsVisible);
                Click(window, manage);
                window.MouseDown(new Point(25, 150), MouseButton.Left);
                window.MouseUp(new Point(25, 150), MouseButton.Left);
                Assert.IsFalse(overlay.IsVisible);
                Click(window, manage); Click(window, logout);
                await Ready(window);
                Assert.IsFalse(overlay.IsVisible);
                Assert.IsFalse(fixture.Auth.LoggedIn);
                Assert.IsNull(Find<ComboBox>(window, "ManagedAiModels").SelectedItem);
                Click(window, manage);
                Assert.IsFalse(logout.IsEffectivelyVisible);
                Click(window, Find<Button>(window, "ManagedAiSignIn"));
                await Ready(window);
                Assert.IsTrue(fixture.Auth.LoggedIn);
                Assert.IsFalse(Find<Button>(window, "ManagedAiConnect").IsVisible);
                Click(window, manage); Click(window, Find<Button>(window, "ManagedAiReconnect"));
                await Ready(window);
                CollectionAssert.AreEqual(new[] { "logout", "login", "logout", "login" }, fixture.Auth.Actions);
                Assert.IsTrue(fixture.Auth.LoggedIn);
                Assert.HasCount(0, fixture.Chat.Requests);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
        TestContext.AddResultFile(screenshot);
    }

    [TestMethod]
    public async Task ReconnectDoesNotStartLoginIfLogoutFails()
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture(); fixture.Auth.FailLogout = true;
            var window = fixture.Create();
            try
            {
                window.Show(); await Ready(window);
                Click(window, Find<Button>(window, "ManagedAiManage"));
                Click(window, Find<Button>(window, "ManagedAiReconnect"));
                await Ready(window);
                CollectionAssert.AreEqual(new[] { "logout" }, fixture.Auth.Actions);
                Assert.IsTrue(fixture.Auth.LoggedIn);
                Assert.Contains("요청을 완료하지 못했습니다", Find<TextBlock>(window, "ManagedAiStatus").Text!);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    [DataRow(640, false, true)]
    [DataRow(900, true, false)]
    public async Task StarterCardsSendImmediatelyWhenConnected(int width, bool dark, bool loggedIn)
    {
        var screenshot = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts", $"chat-starters-{width}-{(dark ? "dark" : "light")}.png");
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var fixture = new Fixture(); fixture.Auth.LoggedIn = loggedIn;
            var window = fixture.Create(); window.Width = width;
            try
            {
                window.Show(); await Ready(window);
                var starters = Controls(window).OfType<Button>().Where(x => x.Classes.Contains("chat-starter")).ToArray();
                Assert.HasCount(3, starters);
                Assert.HasCount(1, Find<StackPanel>(window, "ManagedAiChatTranscript").Children);
                Assert.IsTrue(Find<Border>(window, "ManagedAiPrivacyNotice").IsEffectivelyVisible);
                var bankNames = new[] { "KB국민은행", "신한은행", "하나은행", "우리은행", "NH농협은행" };
                var firstCardName = AutomationProperties.GetName(starters[0]);
                Assert.Contains("찾으려는 서비스 이름을 말씀해 주세요", firstCardName!);
                var bank = bankNames.Single(name => firstCardName!.Contains(name, StringComparison.Ordinal));
                SaveFrame(window, screenshot);
                var input = Find<TextBox>(window, "ManagedAiChatInput");
                if (!loggedIn)
                {
                    Click(window, Find<Button>(window, "ManagedAiConnect"));
                    await Ready(window);
                }
                Click(window, starters[0]);
                await Until(() => fixture.Chat.Requests.Count == 1);
                Assert.Contains(bank, fixture.Chat.Requests[0].Message);
                Assert.Contains("공식 사이트", fixture.Chat.Requests[0].Message);
                Assert.AreEqual(string.Empty, input.Text);
                Assert.IsFalse(Find<StackPanel>(window, "ManagedAiStarters").IsVisible);
                Assert.IsFalse(Find<Border>(window, "ManagedAiPrivacyNotice").IsEffectivelyVisible);
                fixture.Chat.Complete("공식 사이트 안내입니다.");
                await Ready(window);
                Assert.IsFalse(Find<Border>(window, "ManagedAiPrivacyNotice").IsEffectivelyVisible);
                Click(window, Find<Button>(window, "ManagedAiNewChat"));
                await Ready(window);
                Assert.IsTrue(Find<StackPanel>(window, "ManagedAiStarters").IsVisible);
                Assert.IsTrue(Find<Border>(window, "ManagedAiPrivacyNotice").IsEffectivelyVisible);
                Assert.HasCount(1, Find<StackPanel>(window, "ManagedAiChatTranscript").Children);
                Assert.HasCount(3, Controls(window).OfType<Button>().Where(x => x.Classes.Contains("chat-starter")).ToArray());
                Assert.HasCount(0, fixture.Session!.History);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
        TestContext.AddResultFile(screenshot);
    }

    [TestMethod]
    [DataRow(RawInputModifiers.None)]
    [DataRow(RawInputModifiers.Shift)]
    public async Task StarterClickSendsRegardlessOfShift(RawInputModifiers modifiers)
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture();
            var window = fixture.Create();
            try
            {
                window.Show(); await Ready(window);
                var starter = Controls(window).OfType<Button>().First(x => x.Classes.Contains("chat-starter"));
                Find<TextBox>(window, "ManagedAiChatInput").Text = "작성하던 내용";
                Click(window, starter, modifiers);
                await Until(() => fixture.Chat.Requests.Count == 1);
                StringAssert.StartsWith(fixture.Chat.Requests.Single().Message, "작성하던 내용\n\n");
                Assert.Contains("인터넷뱅킹 공식 사이트", fixture.Chat.Requests.Single().Message);
                Assert.AreEqual(string.Empty, Find<TextBox>(window, "ManagedAiChatInput").Text);
                Assert.IsFalse(Find<StackPanel>(window, "ManagedAiStarters").IsVisible);
                fixture.Chat.Complete("공식 사이트 안내입니다.");
                await Ready(window);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task NewChatConfirmsBeforeDiscardingConversationOrDraft()
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture();
            var window = fixture.Create();
            try
            {
                window.Show(); await Ready(window);
                var newChat = Find<Button>(window, "ManagedAiNewChat");
                var transcript = Find<StackPanel>(window, "ManagedAiChatTranscript");
                var input = Find<TextBox>(window, "ManagedAiChatInput");
                Click(window, newChat);
                Assert.AreEqual(0, fixture.Messages.Questions);

                input.Text = "작성 중인 질문";
                fixture.Messages.Answer = AppMessageBoxResult.No;
                Click(window, newChat);
                Assert.AreEqual(1, fixture.Messages.Questions);
                Assert.AreEqual(AppMessageBoxResult.No, fixture.Messages.LastDefault);
                Assert.AreEqual("작성 중인 질문", input.Text);
                fixture.Messages.Answer = AppMessageBoxResult.Yes;
                Click(window, newChat);
                Assert.AreEqual(string.Empty, input.Text);

                input.Text = "첫 질문";
                Click(window, Find<Button>(window, "ManagedAiChatSend"));
                await Until(() => fixture.Chat.Requests.Count == 1);
                fixture.Chat.Complete("첫 답변");
                await Ready(window);
                input.Text = "다음 질문 초안";
                var messageCount = transcript.Children.Count;
                fixture.Messages.Answer = AppMessageBoxResult.No;
                Click(window, newChat);
                Assert.Contains("기존 대화 내용", fixture.Messages.LastQuestion!);
                Assert.AreEqual(messageCount, transcript.Children.Count);
                Assert.AreEqual("다음 질문 초안", input.Text);
                Assert.HasCount(2, fixture.Session!.History);

                fixture.Messages.Answer = AppMessageBoxResult.Yes;
                Click(window, newChat);
                Assert.AreEqual(string.Empty, input.Text);
                Assert.HasCount(0, fixture.Session.History);
                Assert.HasCount(1, transcript.Children);
                Assert.IsTrue(Find<StackPanel>(window, "ManagedAiStarters").IsVisible);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task CertificateSkillRequiresConsentBeforeLocalCliBridgeAndModelTurn()
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture();
            fixture.Skills.Items = [new("tablecloth-certificate-expiry", "Certificate expiry", "Local expiry", "fixture", true)];
            var window = fixture.Create();
            fixture.Messages.OnQuestion = () => Assert.IsTrue(Find<Button>(window, "ManagedAiCancel").IsVisible);
            try
            {
                window.Show(); await Ready(window);
                var input = Find<TextBox>(window, "ManagedAiChatInput");
                input.Text = "공동인증서 만료일을 알려 주세요.";
                fixture.Messages.Answer = AppMessageBoxResult.No;
                Click(window, Find<Button>(window, "ManagedAiChatSend"));
                await Ready(window);
                Assert.AreEqual(1, fixture.Messages.Questions);
                Assert.AreEqual(0, fixture.Certificates.Calls);
                Assert.HasCount(0, fixture.Chat.Requests);
                Assert.Contains("만료일", input.Text!);
                Assert.IsTrue(Find<StackPanel>(window, "ManagedAiStarters").IsVisible);
                Assert.IsTrue(Find<Border>(window, "ManagedAiPrivacyNotice").IsEffectivelyVisible);

                fixture.Messages.Answer = AppMessageBoxResult.Yes;
                Click(window, Find<Button>(window, "ManagedAiChatSend"));
                await Until(() => fixture.Chat.Requests.Count == 1);
                Assert.AreEqual(1, fixture.Certificates.Calls);
                Assert.AreEqual(Certificates.Report, fixture.Chat.Requests[0].LocalCertificateReport);
                Assert.IsFalse(Find<Border>(window, "ManagedAiPrivacyNotice").IsEffectivelyVisible);
                fixture.Chat.Complete("만료 예정 인증서가 있습니다.");
                await Ready(window);
                Assert.IsFalse(Find<Border>(window, "ManagedAiPrivacyNotice").IsEffectivelyVisible);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task CertificateScreenshotLinkIsRejectedBeforeAnyModelTurn()
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture();
            var window = fixture.Create();
            try
            {
                window.Show(); await Ready(window);
                var input = Find<TextBox>(window, "ManagedAiChatInput");
                input.Text = "인증서 스크린샷 https://example.com/screenshot";
                Click(window, Find<Button>(window, "ManagedAiChatSend"));
                await Ready(window);
                Assert.AreEqual(string.Empty, input.Text);
                Assert.HasCount(0, fixture.Chat.Requests);
                Assert.AreEqual(0, fixture.Certificates.Calls);
                Assert.Contains("전송하지 않았습니다", Find<TextBlock>(window, "ManagedAiStatus").Text!);
                Assert.IsTrue(Find<StackPanel>(window, "ManagedAiStarters").IsVisible);
                Assert.IsTrue(Find<Border>(window, "ManagedAiPrivacyNotice").IsEffectivelyVisible);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task PersonalDataIsMaskedInInputAndBeforeModelTurn()
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture();
            var window = fixture.Create();
            try
            {
                window.Show(); await Ready(window);
                var input = Find<TextBox>(window, "ManagedAiChatInput");
                input.Text = "연락처 010-1234-5678, 주민번호 900101-1234567, 주소: 테헤란로 123";
                await Until(() => input.Text!.Contains("[전화번호]") && input.Text.Contains("[주민등록번호]") &&
                    input.Text.Contains("[주소]"));
                Assert.DoesNotContain("010-1234-5678", input.Text!);
                Assert.DoesNotContain("900101", input.Text!);
                Assert.DoesNotContain("테헤란로", input.Text!);
                Click(window, Find<Button>(window, "ManagedAiChatSend"));
                await Until(() => fixture.Chat.Requests.Count == 1);
                Assert.Contains("[전화번호]", fixture.Chat.Requests[0].Message);
                Assert.Contains("[주민등록번호]", fixture.Chat.Requests[0].Message);
                Assert.Contains("[주소]", fixture.Chat.Requests[0].Message);
                fixture.Chat.Complete("확인했습니다.");
                await Ready(window);
                Assert.DoesNotContain("010-1234-5678", fixture.Session!.History[0].Text);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task CertificateInputRetriesTransientRuntimeContention()
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture();
            fixture.Skills.Items = [new("tablecloth-certificate-expiry", "Certificate expiry", "Local expiry", "fixture", true)];
            fixture.Chat.BusyAttempts = 1;
            var window = fixture.Create();
            try
            {
                window.Show(); await Ready(window);
                var input = Find<TextBox>(window, "ManagedAiChatInput");
                input.Text = "이 컴퓨터에서 30일 안에 만료되는 공동인증서가 있는지 확인해 주세요.";
                Click(window, Find<Button>(window, "ManagedAiChatSend"));
                await Until(() => fixture.Chat.Requests.Count == 1);
                Assert.AreEqual(2, fixture.Chat.Attempts);
                Assert.AreEqual(1, fixture.Certificates.Calls);
                Assert.AreEqual(Certificates.Report, fixture.Chat.Requests[0].LocalCertificateReport);
                fixture.Chat.Complete("만료 예정 인증서가 없습니다.");
                await Ready(window);
                Assert.AreEqual(string.Empty, Find<TextBox>(window, "ManagedAiChatInput").Text);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task SandboxSkillUsesHostBridgeBeforeModelTurn()
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture();
            fixture.Skills.Items = [new("tablecloth-windows-sandbox", "Windows Sandbox", "CLI control", "fixture", true)];
            var window = fixture.Create();
            try
            {
                window.Show(); await Ready(window);
                var input = Find<TextBox>(window, "ManagedAiChatInput");
                input.Text = "Windows Sandbox 상태를 알려 주세요.";
                Click(window, Find<Button>(window, "ManagedAiChatSend"));
                await Until(() => fixture.Chat.Requests.Count == 1);
                Assert.AreEqual(SandboxCliAction.List, fixture.Sandbox.Request!.Action);
                Assert.AreEqual(Sandbox.Report, fixture.Chat.Requests[0].LocalWindowsSandboxReport);
                fixture.Chat.Complete("실행 중인 Sandbox가 없습니다.");
                await Ready(window);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task LegacySafeModeSettingDoesNotChangeAiSandboxStart()
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture();
            fixture.Preferences.Json = """{"EnableSafeMode":true,"EnableSandboxPublicDnsFallback":false,"ShareNpkiFolder":true}""";
            fixture.Skills.Items = [new("tablecloth-windows-sandbox", "Windows Sandbox", "CLI control", "fixture", true)];
            var window = fixture.Create();
            try
            {
                window.Show(); await Ready(window);
                var input = Find<TextBox>(window, "ManagedAiChatInput");
                input.Text = "샌드박스를 시작해 주세요.";
                Click(window, Find<Button>(window, "ManagedAiChatSend"));
                await Until(() => fixture.Chat.Requests.Count == 1);
                Assert.IsFalse(fixture.Sandbox.Request!.SafeMode);
                Assert.IsNull(fixture.Messages.LastQuestion);
                fixture.Chat.Complete("샌드박스를 시작했습니다."); await Ready(window);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task SandboxStopKeepsDraftUntilUserAcceptsStateLoss()
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture();
            fixture.Skills.Items = [new("tablecloth-windows-sandbox", "Windows Sandbox", "CLI control", "fixture", true)];
            var window = fixture.Create();
            try
            {
                window.Show(); await Ready(window);
                var input = Find<TextBox>(window, "ManagedAiChatInput");
                input.Text = "샌드박스를 종료해 주세요.";
                fixture.Messages.Answer = AppMessageBoxResult.No;
                Click(window, Find<Button>(window, "ManagedAiChatSend"));
                Assert.IsNull(fixture.Sandbox.Request);
                Assert.HasCount(0, fixture.Chat.Requests);
                Assert.AreEqual("샌드박스를 종료해 주세요.", input.Text);

                fixture.Messages.Answer = AppMessageBoxResult.Yes;
                Click(window, Find<Button>(window, "ManagedAiChatSend"));
                await Until(() => fixture.Chat.Requests.Count == 1);
                Assert.AreEqual(SandboxCliAction.Stop, fixture.Sandbox.Request!.Action);
                fixture.Chat.Complete("Sandbox 종료 결과를 확인했습니다.");
                await Ready(window);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task CompletedLoginHidesInstructionsDuringModelDiscoveryAndIgnoresLateProgress()
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture();
            fixture.Auth.LoggedIn = false;
            fixture.Auth.LoginCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Models.Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var window = fixture.Create();
            try
            {
                window.Show(); await Ready(window);
                Find<Button>(window, "ManagedAiConnect").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Until(() => fixture.Auth.Progress is not null);
                fixture.Auth.Progress!.Report(new(AiLoginStage.AwaitingUser, new("https://auth.openai.com/codex/device"), "ABCD-EFGHI"));
                await Until(() => Find<StackPanel>(window, "ManagedAiLoginInstructions").IsVisible);
                fixture.Auth.LoginCompletion.SetResult();
                await Until(() => fixture.Models.Calls == 1);
                Assert.IsFalse(Find<StackPanel>(window, "ManagedAiLoginInstructions").IsVisible);
                Assert.Contains("모델", Find<TextBlock>(window, "ManagedAiStatus").Text!);
                // Progress<T> can deliver queued login updates while the next operation is awaiting I/O.
                fixture.Auth.Progress.Report(new(AiLoginStage.AwaitingUser, new("https://auth.openai.com/codex/device"), "LATE-CODE"));
                fixture.Auth.Progress.Report(new(AiLoginStage.Completed));
                Dispatcher.UIThread.RunJobs();
                Assert.IsFalse(Find<StackPanel>(window, "ManagedAiLoginInstructions").IsVisible);
                Assert.Contains("모델", Find<TextBlock>(window, "ManagedAiStatus").Text!);
                fixture.Models.Completion.SetResult();
                await Ready(window);
                Assert.IsFalse(Find<Button>(window, "ManagedAiConnect").IsVisible);
                Assert.Contains("대화할 준비", Find<TextBlock>(window, "ManagedAiStatus").Text!);
                Assert.IsFalse(Controls(window).OfType<SelectableTextBlock>().Any(x => x.Text?.Contains("ABCD-EFGHI") == true));
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task FailedModelDiscoveryCanRetryAndAuthenticationFailureReturnsToConnectionFlow()
    {
        var headless = HeadlessTestSession.Shared;
        await headless.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture(); fixture.Models.Fail = true;
            var window = fixture.Create();
            try
            {
                window.Show(); await Ready(window);
                var connect = Find<Button>(window, "ManagedAiConnect");
                Assert.AreEqual("모델 목록 다시 불러오기", connect.Content);
                Assert.IsNull(Find<ComboBox>(window, "ManagedAiModels").SelectedItem);
                fixture.Models.Fail = false;
                connect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Until(() => !connect.IsVisible);
                Find<TextBox>(window, "ManagedAiChatInput").Text = "인증 만료 테스트";
                Find<Button>(window, "ManagedAiChatSend").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Until(() => fixture.Chat.Requests.Count == 1);
                fixture.Chat.Fail(AiFailureCode.AuthenticationRequired);
                await Until(() => connect.IsVisible && connect.IsEnabled);
                Assert.IsNull(Find<ComboBox>(window, "ManagedAiModels").SelectedItem);
                Assert.IsFalse(Controls(window).Any(x => AutomationProperties.GetAutomationId(x) == "ManagedAiPendingResponse"));
                Assert.HasCount(0, fixture.Session!.History);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    [DataRow("ko-KR", false, PhysicalKey.Enter)]
    [DataRow("en-US", true, PhysicalKey.Space)]
    public async Task WelcomeSkillLinkOpensCatalogAndInstallsThroughKeyboard(string culture, bool dark, PhysicalKey activationKey)
    {
        await HeadlessTestSession.Shared.Dispatch<bool>(async () =>
        {
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var fixture = new Fixture();
            var revision = new string('a', 40);
            var entry = new SkillCatalogEntry("fixture-report", "Documents", "Report skill", "Local report instructions.",
                "https://github.com/fixture/skills", "report", revision, "Fixture", "MIT", "upstream", "none", "")
                { LicenseSource = $"https://github.com/fixture/skills/blob/{revision}/LICENSE" };
            fixture.Marketplace.Items = [entry];
            fixture.Marketplace.Skills = fixture.Skills;
            var window = fixture.Create(CultureInfo.GetCultureInfo(culture));
            SkillMarketplaceWindow? child = null;
            try
            {
                window.Show(); await Ready(window);
                var input = Find<TextBox>(window, "ManagedAiChatInput");
                input.Text = "Draft";
                var link = Find<Button>(window, "ManagedAiBrowseSkills");
                Assert.IsTrue(Find<Button>(window, "ManagedAiManage").Focus(NavigationMethod.Tab));
                for (var i = 0; i < 8 && !link.IsFocused; i++)
                {
                    window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                    window.KeyReleaseQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                }
                Assert.IsTrue(link.IsFocused, "The welcome link must participate in keyboard navigation.");
                window.KeyPressQwerty(activationKey, RawInputModifiers.None);
                window.KeyReleaseQwerty(activationKey, RawInputModifiers.None);
                await Until(() => window.OwnedWindows.OfType<SkillMarketplaceWindow>().Any());
                child = window.OwnedWindows.OfType<SkillMarketplaceWindow>().Single();
                Assert.AreSame(window, child.Owner);
                Assert.AreEqual(0, child.FindControl<TabControl>("MarketplaceTabs")!.SelectedIndex);
                var model = (SkillMarketplaceWindowViewModel)child.DataContext!;
                await Until(() => model.CatalogSkills.Count == 1 && !model.LocalBusy && !model.CatalogBusy);
                model.SelectedCatalogSkill = model.CatalogSkills.Single();
                var install = child.FindControl<Button>("CatalogInstall")!;
                Assert.IsTrue(install.IsEffectivelyEnabled);
                Click(child, install);
                await Until(() => model.InstalledSkills.Count == 1 && !model.LocalBusy && !model.CatalogBusy);
                CollectionAssert.AreEqual(new[] { entry.Id }, fixture.Marketplace.InstalledIds);
                Assert.IsTrue(fixture.Skills.Items.Single().Enabled);
                child.Close();
                await Until(() => Find<TextBlock>(window, "ManagedAiActiveSkillCount").Text!
                    .Contains(culture == "ko-KR" ? "활성 스킬 1개" : "1 enabled skills"));
                await Ready(window);
                Assert.AreEqual("Draft", input.Text);
                Assert.HasCount(0, fixture.Chat.Requests);
                Assert.IsTrue(link.IsEnabled);
            }
            finally { child?.Close(); window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    [DataRow("ko-KR", false)]
    [DataRow("en-US", true)]
    public async Task SkillMenuOpensInstalledMarketplaceAndRefreshesChatAfterClosing(string culture, bool dark)
    {
        await HeadlessTestSession.Shared.Dispatch<bool>(async () =>
        {
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var fixture = new Fixture();
            fixture.Skills.Items = [new("fixture-skill", "업무 절차", "Work instructions.",
                Path.Combine(Path.GetTempPath(), "fixture-skill"), true)];
            var window = fixture.Create(CultureInfo.GetCultureInfo(culture));
            SkillMarketplaceWindow? child = null;
            try
            {
                window.Show(); await Ready(window);
                var initialCalls = fixture.Skills.Calls;
                Click(window, Find<Button>(window, "ManagedAiManage"));
                var overlay = Find<Grid>(window, "ManagedAiSettings");
                Assert.IsTrue(overlay.IsVisible);
                Assert.AreEqual(initialCalls, fixture.Skills.Calls);
                Assert.IsFalse(Controls(window).Any(x => AutomationProperties.GetAutomationId(x) is
                    "ManagedAiSkillImport" or "ManagedAiSkillRefresh" or "ManagedAiSkillFolder" or "ManagedAiSkillList"));
                SaveFrame(window, Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts", $"chat-skill-marketplace-entry-{culture}.png"));
                Find<Button>(window, "ManagedAiSkillMarketplace").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Until(() => window.OwnedWindows.OfType<SkillMarketplaceWindow>().Any());
                child = window.OwnedWindows.OfType<SkillMarketplaceWindow>().Single();
                Assert.AreSame(window, child.Owner);
                Assert.AreEqual(1, child.FindControl<TabControl>("MarketplaceTabs")!.SelectedIndex);
                Assert.IsFalse(overlay.IsVisible);
                Assert.IsFalse(Find<Button>(window, "ManagedAiCancel").IsVisible);
                await window.OpenSkillMarketplaceAsync();
                Assert.HasCount(1, window.OwnedWindows);
                var model = (SkillMarketplaceWindowViewModel)child.DataContext!;
                await Until(() => model.InstalledSkills.Count == 1 && !model.LocalBusy && !model.CatalogBusy);
                Assert.Contains(culture == "ko-KR" ? "계속 관리" : "continue managing", model.CatalogNotice);
                model.SelectedInstalledSkill = model.InstalledSkills.Single();
                await model.ToggleSkillCommand.ExecuteAsync(null);
                Assert.IsFalse(fixture.Skills.Items.Single().Enabled);
                child.Close();
                await Until(() => Find<TextBlock>(window, "ManagedAiActiveSkillCount").Text!
                    .Contains(culture == "ko-KR" ? "활성 스킬 0개" : "0 enabled skills"));
                await Ready(window);
                Assert.IsTrue(window.IsVisible);
                Assert.IsFalse(overlay.IsVisible);
                Assert.AreEqual(initialCalls + 2, fixture.Skills.Calls);
                Assert.AreEqual(0, fixture.Messages.Questions);
            }
            finally { child?.Close(); window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task ClosingMarketplaceDuringMutationWaitsForLeaseReleaseBeforeRefreshingChat()
    {
        await HeadlessTestSession.Shared.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture();
            fixture.Skills.Items = [new("fixture-skill", "Skill", "Instructions.", "C:\\fixture", true)];
            var window = fixture.Create();
            SkillMarketplaceWindow? child = null;
            try
            {
                window.Show(); await Ready(window);
                var open = window.OpenSkillMarketplaceAsync();
                await Until(() => window.OwnedWindows.OfType<SkillMarketplaceWindow>().Any());
                child = window.OwnedWindows.OfType<SkillMarketplaceWindow>().Single();
                var model = (SkillMarketplaceWindowViewModel)child.DataContext!;
                await Until(() => model.InstalledSkills.Count == 1 && !model.LocalBusy);
                fixture.Skills.ToggleCleanup = new(TaskCreationOptions.RunContinuationsAsynchronously);
                model.SelectedInstalledSkill = model.InstalledSkills.Single();
                var toggle = model.ToggleSkillCommand.ExecuteAsync(null);
                await Until(() => fixture.Skills.LeaseHeld);
                var beforeClose = fixture.Skills.Calls;
                child.Close();
                await Until(() => Find<Button>(window, "ManagedAiCancel").IsVisible);
                Assert.IsTrue(fixture.Skills.LeaseHeld);
                Assert.AreEqual(beforeClose, fixture.Skills.Calls);
                fixture.Skills.ToggleCleanup.SetResult();
                await toggle;
                await open;
                Assert.AreEqual(0, fixture.Skills.BusyReads);
                Assert.AreEqual(beforeClose + 1, fixture.Skills.Calls);
                Assert.Contains("활성 스킬 0개", Find<TextBlock>(window, "ManagedAiActiveSkillCount").Text!);
            }
            finally { fixture.Skills.ToggleCleanup?.TrySetResult(); child?.Close(); window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    private static IEnumerable<Control> Controls(Window window) => window.GetVisualDescendants().OfType<Control>();
    private static Rect VisibleBounds(Window window, Control control)
    {
        Assert.IsTrue(control.IsEffectivelyVisible, AutomationProperties.GetAutomationId(control));
        var position = control.TranslatePoint(default, window);
        Assert.IsNotNull(position);
        var bounds = new Rect(position.Value, control.Bounds.Size);
        Assert.IsTrue(bounds.Width > 0 && bounds.Height > 0);
        Assert.IsTrue(bounds.Left >= -1 && bounds.Top >= -1 &&
            bounds.Right <= window.ClientSize.Width + 1 && bounds.Bottom <= window.ClientSize.Height + 1,
            $"{AutomationProperties.GetAutomationId(control)} falls outside the visible window: {bounds}.");
        return bounds;
    }

    private static void Click(Window window, Control control, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Dispatcher.UIThread.RunJobs();
        Assert.IsTrue(control.IsEffectivelyVisible);
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window);
        Assert.IsNotNull(point);
        window.MouseDown(point.Value, MouseButton.Left, modifiers);
        window.MouseUp(point.Value, MouseButton.Left, modifiers);
        Dispatcher.UIThread.RunJobs();
    }
    private static void SaveFrame(Window window, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame();
        Assert.IsNotNull(frame); frame.Save(path);
    }
    private static T Find<T>(Window window, string id) where T : Control
        => Controls(window).OfType<T>().Single(x => AutomationProperties.GetAutomationId(x) == id);
    private static Task Ready(Window window) => Until(() =>
        !Find<Button>(window, "ManagedAiCancel").IsVisible &&
        Find<Button>(window, "ManagedAiConnect").Content?.ToString() is not ("연결 확인 중" or "Checking connection"));
    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200; i++)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition()) return;
            await Task.Delay(10);
        }
        Assert.Fail("UI did not reach the expected state.");
    }

    private sealed class Fixture
    {
        public Runtime Runtime = new();
        public Authentication Auth = new();
        public Models Models = new();
        public Skills Skills = new();
        public Marketplace Marketplace = new();
        public Chat Chat = new();
        public Certificates Certificates = new();
        public Sandbox Sandbox = new();
        public Messages Messages = new();
        public Preferences Preferences = new();
        public ManagedAiChatSession? Session;
        public ManagedAiWindow Create(CultureInfo? culture = null)
        {
            CultureInfo.CurrentUICulture = culture ?? CultureInfo.GetCultureInfo("ko-KR");
            return new(Session = new(Chat, new Browser()), Runtime, Auth, Messages, Models, Skills, Preferences, Certificates, Sandbox, Marketplace);
        }
    }
    private sealed class AiWindowProvider(ManagedAiWindow window) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(ManagedAiWindow) ? window : null;
    }
    private sealed class Preferences : IPreferencesManager
    {
        public string Json = "{}";
        public bool? LoadCalledOnUiThread;
        public bool FailSave;
        public TaskCompletionSource? SaveGate;
        public List<string> Saved = [];
        public int MaxConcurrentSaves;
        private int _activeSaves;
        public PreferenceSettings Read() => JsonSerializer.Deserialize(Json, TableClothJsonContext.Default.PreferenceSettings)!;
        public static string Serialize(PreferenceSettings settings) => JsonSerializer.Serialize(settings, TableClothJsonContext.Default.PreferenceSettings);
        public PreferenceSettings GetDefaultPreferences() => new();
        public Task<PreferenceSettings?> LoadPreferencesAsync(CancellationToken token = default)
        { LoadCalledOnUiThread ??= Dispatcher.UIThread.CheckAccess(); return Task.FromResult<PreferenceSettings?>(Read()); }
        public async Task SavePreferencesAsync(PreferenceSettings settings, CancellationToken token = default)
        {
            MaxConcurrentSaves = Math.Max(MaxConcurrentSaves, ++_activeSaves);
            try
            {
                if (SaveGate is not null) await SaveGate.Task.WaitAsync(token);
                if (FailSave) throw new IOException("fixture failure");
                Json = Serialize(settings); Saved.Add(settings.LastSelectedAiModel);
            }
            finally { _activeSaves--; }
        }
    }
    private sealed class Runtime : IManagedRuntimeManager
    {
        public bool Installed = true;
        public int Installs;
        public bool? GetActiveCalledOnUiThread;
        private readonly ManagedRuntime _runtime = new(new("0.156.1", "fixture", "hash"), "fixture", "fixture", "fixture", DateTimeOffset.UtcNow);
        public Task<ManagedRuntime?> GetActiveAsync(CancellationToken token)
        { GetActiveCalledOnUiThread ??= Dispatcher.UIThread.CheckAccess(); return Task.FromResult(Installed ? _runtime : null); }
        public Task<ManagedRuntime> InstallAsync(string? version, IProgress<AiProgress>? progress, CancellationToken token)
        { Installs++; Installed = true; return Task.FromResult(_runtime); }
        public Task<ManagedRuntime> RollbackAsync(CancellationToken token) => throw new NotSupportedException();
    }
    private sealed class Authentication : IProviderAuthentication
    {
        public bool LoggedIn = true;
        public bool? StatusCalledOnUiThread;
        public bool FailLogout;
        public List<string> Actions = [];
        public AiLoginMethod? Method;
        public TaskCompletionSource? LoginCompletion;
        public IProgress<AiLoginUpdate>? Progress;
        public Task<bool> IsLoggedInAsync(CancellationToken token)
        { StatusCalledOnUiThread ??= Dispatcher.UIThread.CheckAccess(); return Task.FromResult(LoggedIn); }
        public Task LoginAsync(CancellationToken token) => throw new NotSupportedException();
        public async Task LoginAsync(AiLoginMethod method, IProgress<AiLoginUpdate>? progress, CancellationToken token)
        {
            Actions.Add("login"); Method = method; Progress = progress;
            if (LoginCompletion is not null) await LoginCompletion.Task.WaitAsync(token);
            LoggedIn = true;
        }
        public Task LogoutAsync(CancellationToken token)
        {
            Actions.Add("logout");
            if (FailLogout) throw new ManagedAiException(AiFailureCode.ProviderFailed);
            LoggedIn = false; return Task.CompletedTask;
        }
    }
    private sealed class Models : IManagedAiModelCatalog
    {
        public IReadOnlyList<AiModel> Items = [new("fixture-default", "기본 모델", true), new("fixture-other", "다른 모델")];
        public bool? ListCalledOnUiThread;
        public int Calls;
        public bool Fail;
        public TaskCompletionSource? Completion;
        public async Task<IReadOnlyList<AiModel>> ListAsync(CancellationToken token)
        {
            ListCalledOnUiThread ??= Dispatcher.UIThread.CheckAccess();
            Calls++;
            if (Fail) throw new ManagedAiException(AiFailureCode.ModelListUnavailable);
            if (Completion is not null) await Completion.Task.WaitAsync(token);
            return Items;
        }
    }
    private sealed class Skills : IManagedAiSkillManager
    {
        public string StorageDirectory => Path.Combine(Path.GetTempPath(), "tablecloth-managed-ai-test-skills");
        public IReadOnlyList<AiSkill> Items = [];
        public bool? ListCalledOnUiThread;
        public int Calls;
        public TaskCompletionSource? ToggleCleanup;
        public bool LeaseHeld;
        public int BusyReads;
        public Task<IReadOnlyList<AiSkill>> ListAsync(CancellationToken token)
        {
            ListCalledOnUiThread ??= Dispatcher.UIThread.CheckAccess(); Calls++;
            if (LeaseHeld) { BusyReads++; throw new ManagedAiException(AiFailureCode.RuntimeBusy); }
            return Task.FromResult(Items);
        }
        public Task<IReadOnlyList<AiSkill>> ImportAsync(string sourceDirectory, CancellationToken token)
            => throw new AssertFailedException("Unexpected skill import");
        public async Task<IReadOnlyList<AiSkill>> SetEnabledAsync(string id, bool enabled, CancellationToken token)
        {
            Items = Items.Select(x => x.Id == id ? x with { Enabled = enabled } : x).ToArray();
            if (ToggleCleanup is not null)
            {
                LeaseHeld = true;
                try { await Task.Delay(Timeout.Infinite, token); }
                finally { await ToggleCleanup.Task; LeaseHeld = false; }
            }
            return Items;
        }
        public Task<IReadOnlyList<AiSkill>> RemoveAsync(string id, CancellationToken token)
        {
            Items = Items.Where(x => x.Id != id).ToArray();
            return Task.FromResult(Items);
        }
    }
    private sealed class Marketplace : IManagedAiSkillMarketplace
    {
        public IReadOnlyList<SkillCatalogEntry>? Items;
        public Skills? Skills;
        public readonly List<string> InstalledIds = [];
        public Task<IReadOnlyList<SkillCatalogEntry>> ListAsync(CancellationToken token)
            => Items is not null ? Task.FromResult(Items) : throw new ManagedAiException(AiFailureCode.CatalogUnavailable);
        public Task<IReadOnlyList<AiSkill>> InstallAsync(string id, CancellationToken token)
        {
            if (Skills is null || Items is null) throw new AssertFailedException("Unexpected remote skill install");
            var entry = Items.Single(x => x.Id == id);
            Skills.Items = Skills.Items.Append(new AiSkill(id, entry.DisplayName, entry.Description,
                Path.Combine(Skills.StorageDirectory, id), true)
                { Source = new(id, entry.Repository, entry.SourcePath, entry.SourceRef) }).ToArray();
            InstalledIds.Add(id);
            return Task.FromResult(Skills.Items);
        }
        public Task<IReadOnlyList<AiSkill>> UpdateAsync(string id, CancellationToken token)
            => throw new AssertFailedException("Unexpected remote skill update");
    }
    private sealed class Chat : IManagedAiChatProvider
    {
        public readonly List<AiChatRequest> Requests = [];
        public int BusyAttempts;
        public int Attempts;
        public IProgress<AiProgress>? Progress;
        private TaskCompletionSource<AiChatResponse>? _response;
        public Task<AiChatResponse> ChatAsync(AiChatRequest request, IProgress<AiProgress>? progress, CancellationToken token)
        {
            Attempts++;
            if (BusyAttempts-- > 0) throw new ManagedAiException(AiFailureCode.RuntimeBusy);
            Requests.Add(request); Progress = progress;
            _response = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return _response.Task.WaitAsync(token);
        }
        public void Complete(string message, IReadOnlyList<AiSkillExecutionRecord>? skillExecutions = null,
            bool skillExecutionsTruncated = false) => _response!.SetResult(new(message, DateTimeOffset.UtcNow, 2, Requests[^1].Model)
            { SkillExecutions = skillExecutions ?? [], SkillExecutionsTruncated = skillExecutionsTruncated });
        public void Fail(AiFailureCode code) => _response!.SetException(new ManagedAiException(code));
    }
    private sealed class Certificates : IManagedAiCertificateBridge
    {
        public const string Report = "{\"rootFound\":true,\"withinDays\":30,\"expiredCount\":0,\"expiringCount\":0,\"scanIncomplete\":false}";
        public int Calls;
        public Task<string> GetExpiryReportAsync(CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(Report); }
    }
    private sealed class Sandbox : IManagedAiSandboxBridge
    {
        public const string Report = """{"Action":"list","Status":"succeeded","ExitCode":0,"Output":"{}"}""";
        public SandboxCliRequest? Request;
        public Task<string> ExecuteAsync(SandboxCliRequest request, CancellationToken cancellationToken)
        { Request = request; return Task.FromResult(Report); }
    }
    private sealed class Browser : ITableClothBrowser
    {
        public Task OpenAsync(Uri target, CancellationToken token) => throw new AssertFailedException("Unexpected browser launch");
    }
    private sealed class Messages : IAppMessageBox
    {
        public int Questions;
        public Action? OnQuestion;
        public AppMessageBoxResult Answer = AppMessageBoxResult.Yes;
        public AppMessageBoxResult LastDefault;
        public string? LastQuestion;
        public AppMessageBoxResult DisplayQuestion(string message, AppMessageBoxButton buttons, AppMessageBoxResult answer)
        { Questions++; LastDefault = answer; LastQuestion = message; OnQuestion?.Invoke(); return Answer; }
        public AppMessageBoxResult DisplayInfo(string message, AppMessageBoxButton buttons) => throw new NotSupportedException();
        public AppMessageBoxResult DisplayError(Exception? reason, bool critical, string file, string member, int line) => throw new NotSupportedException();
        public AppMessageBoxResult DisplayError(string? message, bool critical, string file, string member, int line) => throw new NotSupportedException();
    }
}
