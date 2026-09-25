using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TableCloth.ManagedAi;

namespace TableCloth.Test;

[TestClass, DoNotParallelize]
public sealed class ManagedAiLinkOpenChoiceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(false, "ManagedAiLinkCurrentBrowser", ManagedAiLinkOpenDestination.CurrentBrowser)]
    [DataRow(true, "ManagedAiLinkWindowsSandbox", ManagedAiLinkOpenDestination.WindowsSandbox)]
    public async Task DialogShowsDestinationAndReturnsClickedChoice(bool dark, string buttonId,
        ManagedAiLinkOpenDestination expected)
    {
        var screenshot = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts",
            $"link-open-choice-{(dark ? "dark" : "light")}.png");
        using var session = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
        await session.Dispatch<bool>(async () =>
        {
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var owner = new Window { Width = 640, Height = 540 };
            var dialog = new ManagedAiLinkOpenWindow(new("https://bank.example/product?q=1#section"));
            try
            {
                owner.Show();
                var result = dialog.ShowDialog<ManagedAiLinkOpenDestination>(owner);
                await Until(() => dialog.IsVisible);
                var controls = dialog.GetVisualDescendants().OfType<Control>().ToArray();
                Assert.AreEqual("https://bank.example/product?q=1#section",
                    controls.OfType<SelectableTextBlock>().Single(x => Id(x) == "ManagedAiLinkTarget").Text);
                Assert.HasCount(3, controls.OfType<Button>());
                Assert.IsTrue(controls.OfType<Button>().Single(x => Id(x) == "ManagedAiLinkWindowsSandbox").IsDefault);
                Assert.IsTrue(controls.OfType<Button>().Single(x => Id(x) == "ManagedAiLinkCancel").IsCancel);
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Directory.CreateDirectory(Path.GetDirectoryName(screenshot)!);
                using (var frame = dialog.CaptureRenderedFrame()) { Assert.IsNotNull(frame); frame.Save(screenshot); }
                controls.OfType<Button>().Single(x => Id(x) == buttonId)
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.AreEqual(expected, await result);
            }
            finally { if (dialog.IsVisible) dialog.Close(); owner.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
        TestContext.AddResultFile(screenshot);
    }

    [TestMethod]
    public async Task ClosingDialogReturnsCancel()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
        await session.Dispatch<bool>(async () =>
        {
            var owner = new Window();
            var dialog = new ManagedAiLinkOpenWindow(new("https://example.com/"));
            try
            {
                owner.Show();
                var result = dialog.ShowDialog<ManagedAiLinkOpenDestination>(owner);
                await Until(() => dialog.IsVisible);
                dialog.Close();
                Assert.AreEqual(ManagedAiLinkOpenDestination.Cancel, await result);
            }
            finally { owner.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    private static string? Id(Control control) => AutomationProperties.GetAutomationId(control);

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 100; i++)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition()) return;
            await Task.Delay(10);
        }
        Assert.Fail("Dialog did not reach the expected state.");
    }
}
