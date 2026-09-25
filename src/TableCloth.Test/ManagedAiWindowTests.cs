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
using TableCloth.ManagedAi;
using TableCloth.Models;
using TableCloth.Models.Configuration;
using TableCloth.Serialization;
using TableCloth.ManagedAi.OpenAi;
using System.Text.Json;
using TableCloth.Theme.Controls;

namespace TableCloth.Test;

[TestClass, DoNotParallelize]
public sealed class ManagedAiWindowTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ModelChoiceSurvivesReopeningAndRetainsOtherPreferences()
    {
        var screenshot = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts", "chat-model-cost-640-light.png");
        using var headless = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
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
                Assert.Contains("크레딧", Find<TextBlock>(window, "ManagedAiModelHint").Text!);
                SaveFrame(window, screenshot);
                // A separate settings edit after opening the window must not be overwritten.
                var settings = fixture.Preferences.Read(); settings.Favorites.Add("added-later");
                fixture.Preferences.Json = Preferences.Serialize(settings);
                models.SelectedIndex = 0;
                await Until(() => fixture.Preferences.Read().LastSelectedAiModel == "gpt-6-astra");
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
        using var headless = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
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
        using var headless = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
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
                Assert.IsFalse(Find<TextBlock>(window, "ManagedAiPreferenceNotice").IsVisible);
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
        using var headless = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
        // Explicit Task<bool> dispatch awaits the whole async test; Dispatch<Task> would return a nested task.
        await headless.Dispatch<bool>(async () =>
        {
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var fixture = new Fixture();
            var window = fixture.Create();
            window.Width = width;
            try
            {
                Assert.AreEqual("식탁보 AI (Preview)", window.Title);
                window.Show();
                await Ready(window);
                Assert.IsFalse(Find<Button>(window, "ManagedAiConnect").IsVisible);
                var models = Find<ComboBox>(window, "ManagedAiModels");
                Assert.AreEqual("fixture-default", ((AiModel)models.SelectedItem!).Id);
                models.SelectedIndex = 1;
                var input = Find<TextBox>(window, "ManagedAiChatInput");
                input.Focus(); input.Text = "첫 줄"; input.CaretIndex = input.Text.Length;
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
                Assert.HasCount(3, copies); // Welcome, user, assistant.
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
        using var headless = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
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
                await Until(() => !connect.IsVisible);
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
    [DataRow(640, false)]
    [DataRow(900, true)]
    public async Task SettingsPanelRendersAndSupportsLogoutLoginAndReconnect(int width, bool dark)
    {
        var screenshot = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts", $"chat-settings-{width}-{(dark ? "dark" : "light")}.png");
        using var headless = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
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
        using var headless = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
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
    public async Task StarterPromptsPreserveDraftAndWaitForExplicitSend(int width, bool dark, bool loggedIn)
    {
        var screenshot = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts", $"chat-starters-{width}-{(dark ? "dark" : "light")}.png");
        using var headless = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
        await headless.Dispatch<bool>(async () =>
        {
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var fixture = new Fixture(); fixture.Auth.LoggedIn = loggedIn;
            var window = fixture.Create(); window.Width = width;
            try
            {
                window.Show(); await Ready(window);
                var starters = Controls(window).OfType<Button>().Where(x => x.Classes.Contains("chat-starter")).ToArray();
                Assert.HasCount(4, starters);
                SaveFrame(window, screenshot);
                var input = Find<TextBox>(window, "ManagedAiChatInput");
                Click(window, starters[0]);
                Assert.Contains("주민등록등본", input.Text!);
                Assert.HasCount(0, fixture.Chat.Requests);
                input.Text = "작성하던 내용";
                Click(window, starters[1]);
                StringAssert.StartsWith(input.Text!, "작성하던 내용\n\n");
                Assert.Contains("인터넷뱅킹", input.Text!);
                Assert.HasCount(0, fixture.Chat.Requests);
                input.Text = new string('가', 3995);
                Click(window, starters[0]);
                Assert.AreEqual(3995, input.Text!.Length);
                input.Text = "공식 사이트를 알려주세요.";
                if (!loggedIn)
                {
                    Assert.IsFalse(Find<Button>(window, "ManagedAiChatSend").IsEnabled);
                    Click(window, Find<Button>(window, "ManagedAiConnect"));
                    await Ready(window);
                }
                Click(window, Find<Button>(window, "ManagedAiChatSend"));
                await Until(() => fixture.Chat.Requests.Count == 1);
                Assert.IsFalse(Find<StackPanel>(window, "ManagedAiStarters").IsVisible);
                fixture.Chat.Complete("공식 사이트 안내입니다.");
                await Ready(window);
                Click(window, Find<Button>(window, "ManagedAiNewChat"));
                await Ready(window);
                Assert.IsTrue(Find<StackPanel>(window, "ManagedAiStarters").IsVisible);
                Assert.HasCount(4, Controls(window).OfType<Button>().Where(x => x.Classes.Contains("chat-starter")).ToArray());
                Assert.HasCount(0, fixture.Session!.History);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
        TestContext.AddResultFile(screenshot);
    }

    [TestMethod]
    public async Task CompletedLoginHidesInstructionsDuringModelDiscoveryAndIgnoresLateProgress()
    {
        using var headless = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
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
        using var headless = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
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

    private static IEnumerable<Control> Controls(Window window) => window.GetVisualDescendants().OfType<Control>();
    private static void Click(Window window, Control control)
    {
        Dispatcher.UIThread.RunJobs();
        Assert.IsTrue(control.IsEffectivelyVisible);
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window);
        Assert.IsNotNull(point);
        window.MouseDown(point.Value, MouseButton.Left);
        window.MouseUp(point.Value, MouseButton.Left);
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
    private static Task Ready(Window window) => Until(() => !Find<Button>(window, "ManagedAiCancel").IsVisible);
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
        public Chat Chat = new();
        public Messages Messages = new();
        public Preferences Preferences = new();
        public ManagedAiChatSession? Session;
        public ManagedAiWindow Create() => new(Session = new(Chat, new Browser()), Runtime, Auth, Messages, Models, Preferences);
    }
    private sealed class Preferences : IPreferencesManager
    {
        public string Json = "{}";
        public bool FailSave;
        public TaskCompletionSource? SaveGate;
        public List<string> Saved = [];
        public int MaxConcurrentSaves;
        private int _activeSaves;
        public PreferenceSettings Read() => JsonSerializer.Deserialize(Json, TableClothJsonContext.Default.PreferenceSettings)!;
        public static string Serialize(PreferenceSettings settings) => JsonSerializer.Serialize(settings, TableClothJsonContext.Default.PreferenceSettings);
        public PreferenceSettings GetDefaultPreferences() => new();
        public Task<PreferenceSettings?> LoadPreferencesAsync(CancellationToken token = default) => Task.FromResult<PreferenceSettings?>(Read());
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
        private readonly ManagedRuntime _runtime = new(new("1.2.3", "fixture", "hash"), "fixture", "fixture", "fixture", DateTimeOffset.UtcNow);
        public Task<ManagedRuntime?> GetActiveAsync(CancellationToken token) => Task.FromResult(Installed ? _runtime : null);
        public Task<ManagedRuntime> InstallAsync(string? version, IProgress<AiProgress>? progress, CancellationToken token)
        { Installs++; Installed = true; return Task.FromResult(_runtime); }
        public Task<ManagedRuntime> RollbackAsync(CancellationToken token) => throw new NotSupportedException();
    }
    private sealed class Authentication : IProviderAuthentication
    {
        public bool LoggedIn = true;
        public bool FailLogout;
        public List<string> Actions = [];
        public AiLoginMethod? Method;
        public TaskCompletionSource? LoginCompletion;
        public IProgress<AiLoginUpdate>? Progress;
        public Task<bool> IsLoggedInAsync(CancellationToken token) => Task.FromResult(LoggedIn);
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
        public int Calls;
        public bool Fail;
        public TaskCompletionSource? Completion;
        public async Task<IReadOnlyList<AiModel>> ListAsync(CancellationToken token)
        {
            Calls++;
            if (Fail) throw new ManagedAiException(AiFailureCode.ModelListUnavailable);
            if (Completion is not null) await Completion.Task.WaitAsync(token);
            return Items;
        }
    }
    private sealed class Chat : IManagedAiChatProvider
    {
        public readonly List<AiChatRequest> Requests = [];
        public IProgress<AiProgress>? Progress;
        private TaskCompletionSource<AiChatResponse>? _response;
        public Task<AiChatResponse> ChatAsync(AiChatRequest request, IProgress<AiProgress>? progress, CancellationToken token)
        {
            Requests.Add(request); Progress = progress;
            _response = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return _response.Task.WaitAsync(token);
        }
        public void Complete(string message) => _response!.SetResult(new(message, DateTimeOffset.UtcNow, 2, Requests[^1].Model));
        public void Fail(AiFailureCode code) => _response!.SetException(new ManagedAiException(code));
    }
    private sealed class Browser : ITableClothBrowser
    {
        public Task OpenAsync(Uri target, CancellationToken token) => throw new AssertFailedException("Unexpected browser launch");
    }
    private sealed class Messages : IAppMessageBox
    {
        public int Questions;
        public AppMessageBoxResult DisplayQuestion(string message, AppMessageBoxButton buttons, AppMessageBoxResult answer)
        { Questions++; return AppMessageBoxResult.Yes; }
        public AppMessageBoxResult DisplayInfo(string message, AppMessageBoxButton buttons) => throw new NotSupportedException();
        public AppMessageBoxResult DisplayError(Exception? reason, bool critical, string file, string member, int line) => throw new NotSupportedException();
        public AppMessageBoxResult DisplayError(string? message, bool critical, string file, string member, int line) => throw new NotSupportedException();
    }
}
