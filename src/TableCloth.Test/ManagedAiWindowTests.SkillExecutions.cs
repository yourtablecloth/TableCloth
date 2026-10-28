using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Globalization;
using TableCloth.ManagedAi;

namespace TableCloth.Test;

public sealed partial class ManagedAiWindowTests
{
    [TestMethod]
    [DataRow("en-US", false)]
    [DataRow("en-US", true)]
    [DataRow("ko-KR", false)]
    [DataRow("ko-KR", true)]
    public async Task ResponseShowsSeparateAccessibleSkillExecutionRecordsInBothThemes(string culture, bool dark)
    {
        var screenshot = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts",
            $"skill-executions-{culture}-{(dark ? "dark" : "light")}.png");
        await WithSkillExecutionWindow(async (window, fixture) =>
        {
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            window.Width = 640;
            window.Height = 540;
            Assert.IsFalse(Controls(window).Any(control => AutomationProperties.GetAutomationId(control) == "ManagedAiSkillExecutionSection"));
            var records = new[]
            {
                Execution("call-success", AiSkillExecutionStatus.Completed, 0, true),
                Execution("call-failed", AiSkillExecutionStatus.Failed, 7, true) with { Failure = "ScriptFailed" }
            };
            Find<TextBox>(window, "ManagedAiChatInput").Text = "Run the installed skill.";
            Click(window, Find<Button>(window, "ManagedAiChatSend"));
            await Until(() => fixture.Chat.Requests.Count == 1);
            var progress = fixture.Chat.Progress!;
            progress.Report(new AiProgress("SkillExecution") { SkillExecution = records[1] });
            var failed = culture == "ko-KR" ? "실패" : "Failed";
            await Until(() => Find<TextBlock>(window, "ManagedAiStatus").Text?.Contains($"alpha-skill: {failed}") == true);
            fixture.Chat.Complete("Model-only response.", records, skillExecutionsTruncated: true);
            await Ready(window);
            Dispatcher.UIThread.RunJobs();

            var section = Find<Border>(window, "ManagedAiSkillExecutionSection");
            Assert.AreEqual(culture == "ko-KR" ? "앱 실행 기록" : "App execution records", AutomationProperties.GetName(section));
            Assert.Contains(culture == "ko-KR" ? "브로커" : "broker", AutomationProperties.GetHelpText(section)!);
            Assert.IsFalse(string.IsNullOrWhiteSpace(ToolTip.GetTip(section)?.ToString()));
            var rows = section.GetVisualDescendants().OfType<StackPanel>()
                .Where(row => AutomationProperties.GetAutomationId(row) == "ManagedAiSkillExecutionRecord").ToArray();
            Assert.HasCount(2, rows);
            foreach (var row in rows)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(AutomationProperties.GetName(row)));
                var text = string.Join("\n", row.GetVisualDescendants().OfType<TextBlock>().Select(item => item.Text));
                Assert.Contains("alpha-skill", text);
                Assert.Contains("fetch_report [scripts/report.py]", text);
                Assert.Contains(culture == "ko-KR" ? "AppContainer: 브로커 확인" : "AppContainer: broker confirmed", text);
                Assert.DoesNotContain("ScriptFailed", text);
            }
            var allText = string.Join("\n", rows.Select(row => AutomationProperties.GetName(row)));
            Assert.Contains(culture == "ko-KR" ? "성공" : "Succeeded", allText);
            Assert.Contains(failed, allText);
            Assert.Contains(culture == "ko-KR" ? "종료 코드: 7" : "Exit code: 7", allText);
            Assert.IsFalse(string.IsNullOrWhiteSpace(Find<TextBlock>(window, "ManagedAiSkillExecutionsTruncated").Text));
            VisibleBounds(window, section);
            SaveFrame(window, screenshot);

            var status = Find<TextBlock>(window, "ManagedAiStatus").Text;
            progress.Report(new AiProgress("SkillExecution") { SkillExecution = records[0] });
            Dispatcher.UIThread.RunJobs();
            Assert.AreEqual(status, Find<TextBlock>(window, "ManagedAiStatus").Text, "Completed turns must ignore stale progress.");
        }, culture);
        TestContext.AddResultFile(screenshot);
    }

    [TestMethod]
    public Task SkillSelectionAndModelClaimDoNotCreateApplicationExecutionEvidence()
        => WithSkillExecutionWindow(async (window, fixture) =>
        {
            Click(window, Find<Button>(window, "ManagedAiPromptAddSkill"));
            Find<ComboBox>(window, "ManagedAiPromptSkills").SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("$alpha-skill", Find<TextBox>(window, "ManagedAiChatInput").Text!);
            Click(window, Find<Button>(window, "ManagedAiChatSend"));
            await Until(() => fixture.Chat.Requests.Count == 1);
            fixture.Chat.Complete("I ran alpha-skill successfully.");
            await Ready(window);
            Assert.AreEqual("No AppContainer script execution was verified for this response.",
                Find<TextBlock>(window, "ManagedAiSkillExecutionNone").Text);
            Assert.IsFalse(Controls(window).Any(control => AutomationProperties.GetAutomationId(control) == "ManagedAiSkillExecutionRecord"));
            Assert.HasCount(0, fixture.Session!.History.Last().SkillExecutions);
        });

    [TestMethod]
    public Task SkillExecutionMetadataReachesFollowupWithoutChangingModelTextOrCopy()
        => WithSkillExecutionWindow(async (window, fixture) =>
        {
            var record = Execution("call-followup", AiSkillExecutionStatus.Completed, 0, true);
            Find<TextBox>(window, "ManagedAiChatInput").Text = "Run the report.";
            Click(window, Find<Button>(window, "ManagedAiChatSend"));
            await Until(() => fixture.Chat.Requests.Count == 1);
            fixture.Chat.Complete("Model-only response.", [record]);
            await Ready(window);
            var copy = Controls(window).OfType<Button>().Last(button => button.Classes.Contains("chat-copy"));
            Click(window, copy);
            await Until(() => Equals(copy.Content, "Copied"));
            Assert.AreEqual("Model-only response.", await window.Clipboard!.TryGetTextAsync());
            Find<TextBox>(window, "ManagedAiChatInput").Text = "Which tool ran?";
            Click(window, Find<Button>(window, "ManagedAiChatSend"));
            await Until(() => fixture.Chat.Requests.Count == 2);
            var previousAnswer = fixture.Chat.Requests[1].History.Single(message => message.Role == AiChatRole.Assistant);
            Assert.AreEqual("Model-only response.", previousAnswer.Text);
            Assert.AreEqual(record, previousAnswer.SkillExecutions.Single());
            fixture.Chat.Complete("The application recorded fetch_report.");
            await Ready(window);
            Assert.AreEqual("No AppContainer script execution was verified for this response.",
                Find<TextBlock>(window, "ManagedAiSkillExecutionNone").Text);
        });

    [TestMethod]
    [DataRow(AiSkillExecutionStatus.Blocked, "Blocked", false)]
    [DataRow(AiSkillExecutionStatus.Unverified, "Unverified", false)]
    [DataRow(AiSkillExecutionStatus.Failed, "Failed", true)]
    public Task SkillExecutionFailuresNeverDisplaySuccess(AiSkillExecutionStatus status, string expected, bool timedOut)
        => WithSkillExecutionWindow(async (window, fixture) =>
        {
            var record = Execution("call-status", status, null, false) with { TimedOut = timedOut, OutputLimited = timedOut };
            Find<TextBox>(window, "ManagedAiChatInput").Text = "Run the report.";
            Click(window, Find<Button>(window, "ManagedAiChatSend"));
            await Until(() => fixture.Chat.Requests.Count == 1);
            fixture.Chat.Complete("Model-only response.", [record]);
            await Ready(window);
            var row = Find<StackPanel>(window, "ManagedAiSkillExecutionRecord");
            var accessible = AutomationProperties.GetName(row)!;
            Assert.Contains(expected, accessible);
            Assert.Contains("AppContainer: unverified", accessible);
            Assert.Contains("Exit code unavailable", accessible);
            Assert.DoesNotContain("Succeeded", accessible);
            if (timedOut)
            {
                Assert.Contains("Timed out", accessible);
                Assert.Contains("Output limit exceeded", accessible);
            }
        });

    private static AiSkillExecutionRecord Execution(string call, AiSkillExecutionStatus status, int? exit, bool appContainer)
        => new(call, "fetch_report", "directory-alpha", "alpha-skill", "scripts/report.py", status, exit,
            appContainer, false, false, null);

    private static Task WithSkillExecutionWindow(Func<ManagedAiWindow, Fixture, Task> action, string culture = "en-US")
        => HeadlessTestSession.Shared.Dispatch<bool>(async () =>
        {
            var fixture = new Fixture();
            fixture.Skills.Items = [new("directory-alpha", "alpha-skill", "Alpha report skill", "fixture-alpha", true)];
            var window = fixture.Create(CultureInfo.GetCultureInfo(culture));
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
