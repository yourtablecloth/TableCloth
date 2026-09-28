using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TableCloth.ManagedAi;

namespace TableCloth.Test;

public sealed class MarkdownTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TableClothApplication>()
        .UseSkia().WithInterFont().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

[TestClass, DoNotParallelize]
public sealed class ChatMarkdownViewTests
{
    public TestContext TestContext { get; set; } = null!;
    private const string BubbleSample =
        "직장인 연말정산은 보통 회사에 서류를 제출해 처리하고, 공제 자료는 국세청 " +
        "[홈택스 연말정산 간소화 서비스](https://www.hometax.go.kr/)에서 조회하시면 됩니다. " +
        "모바일에서는 [손택스](https://www.hometax.go.kr/) 앱을 이용할 수 있습니다. " +
        "회사에 제출하는 방식과 기간은 회사 담당자에게 확인해 주세요.";
    private const string Sample = """
        # 식탁보 서비스 안내

        **굵게**, *기울임*, ~~취소선~~과 `inline code`를 표시합니다.
        [공식 **홈페이지**][official]에서 안내를 확인합니다.

        > 링크를 누르면 Catalog 서비스를 조회하고 필요한 소프트웨어를 설치합니다.

        3. 서비스 선택
           - 중첩 목록과 **강조**
        4. 설치 후 원래 페이지 열기

        | 서비스 | 설치 항목 | 접속 |
        | :--- | ---: | :---: |
        | 개인뱅킹 | 3개 | [은행](https://bank.example/product?a=1&b=2) |
        | 일반 웹사이트 | 없음 | https://example.com/ |

        ```csharp
        // https://code.example/ is plain code, not a link
        Console.WriteLine("Hello TableCloth");
        ```

        - [x] Catalog 확인
        - [ ] Sandbox 실행

        강제 줄 바꿈\
        다음 줄과 &amp; 문자입니다.

        [official]: https://yourtablecloth.app/ "식탁보"
        """;

    [TestMethod]
    public async Task InlineLinksFitWithinChatBubble()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
        var screenshot = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts", "markdown-chat-bubble-900.png");
        await session.Dispatch(() =>
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
            var body = new StackPanel { Spacing = 8 };
            body.Children.Add(new TextBlock { Text = "식탁보 / gpt-6-luna", FontSize = 12 });
            var clicked = new List<Uri>();
            var markdown = new ChatMarkdownView(BubbleSample, clicked.Add);
            body.Children.Add(markdown);
            var bubble = new Border
            {
                Child = body, Padding = new Thickness(16, 12), CornerRadius = new CornerRadius(12),
                MaxWidth = 690, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                BorderThickness = new Thickness(1), BorderBrush = Avalonia.Media.Brushes.LightGray,
                Background = Avalonia.Media.Brushes.White,
            };
            var transcript = new StackPanel { Margin = new Thickness(20) };
            transcript.Children.Add(bubble);
            var window = new Window { Width = 900, Height = 250, Content = transcript };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                var links = markdown.GetVisualDescendants().OfType<Button>()
                    .Where(button => button.Classes.Contains("markdown-link")).ToArray();
                Assert.HasCount(2, links);
                var paragraph = markdown.Children.OfType<SelectableTextBlock>().Single();
                Assert.IsGreaterThanOrEqualTo(2, paragraph.TextLayout.TextLines.Count);
                var firstLine = paragraph.TextLayout.TextLines[0];
                foreach (var line in paragraph.TextLayout.TextLines)
                {
                    Assert.IsLessThan(0.01, Math.Abs(line.Height - firstLine.Height));
                    Assert.IsLessThan(0.01, Math.Abs(line.Baseline - firstLine.Baseline));
                }
                foreach (var link in links)
                {
                    var caption = link.GetVisualDescendants().OfType<TextBlock>().Single();
                    Assert.IsLessThan(0.01, Math.Abs(TextBlock.GetBaselineOffset(link) - caption.TextLayout.Baseline));
                }
                var lastLinkBottom = links[^1].TranslatePoint(new Point(0, links[^1].Bounds.Height), bubble);
                Assert.IsNotNull(lastLinkBottom);
                Assert.IsGreaterThanOrEqualTo(12d, bubble.Bounds.Height - lastLinkBottom.Value.Y);
                Directory.CreateDirectory(Path.GetDirectoryName(screenshot)!);
                using var frame = window.CaptureRenderedFrame();
                Assert.IsNotNull(frame);
                frame.Save(screenshot);
                var clickPoint = links[^1].TranslatePoint(
                    new Point(links[^1].Bounds.Width / 2, links[^1].Bounds.Height / 2), window);
                Assert.IsNotNull(clickPoint);
                window.MouseDown(clickPoint.Value, MouseButton.Left);
                window.MouseUp(clickPoint.Value, MouseButton.Left);
                Assert.HasCount(1, clicked);

                var firstCaption = links[0].GetVisualDescendants().OfType<TextBlock>().Single();
                var initialBaseline = TextBlock.GetBaselineOffset(links[0]);
                firstCaption.FontSize = 20;
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                var updatedBaseline = TextBlock.GetBaselineOffset(links[0]);
                Assert.AreNotEqual(initialBaseline, updatedBaseline);
                Assert.IsLessThan(0.01, Math.Abs(updatedBaseline - firstCaption.TextLayout.Baseline));

                var laterLink = new ChatMarkdownView("첫 줄입니다.  \n둘째 줄 [링크](https://example.com/) 다음 글", _ => { });
                var laterBubble = new Border { Child = laterLink, Padding = new Thickness(16, 12) };
                transcript.Children.Add(laterBubble);
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                var laterParagraph = laterLink.Children.OfType<SelectableTextBlock>().Single();
                var laterButton = laterLink.GetVisualDescendants().OfType<Button>().Single();
                Assert.HasCount(2, laterParagraph.TextLayout.TextLines);
                Assert.IsLessThan(0.01, Math.Abs(laterParagraph.TextLayout.TextLines[0].Baseline -
                    laterParagraph.TextLayout.TextLines[1].Baseline));
                var finalLinkBottom = laterButton.TranslatePoint(new Point(0, laterButton.Bounds.Height), laterBubble);
                Assert.IsNotNull(finalLinkBottom);
                Assert.IsGreaterThanOrEqualTo(12d, laterBubble.Bounds.Height - finalLinkBottom.Value.Y);
            }
            finally { window.Close(); }
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
        TestContext.AddResultFile(screenshot);
    }

    [TestMethod]
    [DataRow(640, false)]
    [DataRow(900, true)]
    public async Task RendersMarkdownAndRoutesOnlyClickedWebLinks(int width, bool dark)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
        var directory = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"markdown-{width}-{(dark ? "dark" : "light")}.png");
        await session.Dispatch(() =>
        {
            Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var clicked = new List<Uri>();
            var view = new ChatMarkdownView(Sample, clicked.Add) { Margin = new Thickness(24), MaxWidth = 690 };
            var window = new Window { Width = width, Height = 960, Content = new ScrollViewer { Content = view } };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                var visuals = view.GetVisualDescendants().OfType<Control>().ToArray();
                Assert.HasCount(1, visuals.Where(x => x.Classes.Contains("markdown-heading")).ToArray());
                Assert.HasCount(1, visuals.Where(x => x.Classes.Contains("markdown-table")).ToArray());
                Assert.HasCount(1, visuals.Where(x => x.Classes.Contains("markdown-code")).ToArray());
                Assert.HasCount(2, visuals.OfType<CheckBox>().ToArray());
                var links = visuals.OfType<Button>().Where(x => x.Classes.Contains("markdown-link")).ToArray();
                Assert.HasCount(3, links);
                Assert.IsFalse(links.Any(x => AutomationProperties.GetName(x)?.Contains("code.example") == true));
                Assert.HasCount(0, clicked);
                var bank = links.Single(x => AutomationProperties.GetName(x)!.Contains("bank.example"));
                bank.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.AreEqual("https://bank.example/product?a=1&b=2", clicked.Single().OriginalString);
                Assert.IsLessThanOrEqualTo(width - 48, view.Bounds.Width);
                using var frame = window.CaptureRenderedFrame();
                Assert.IsNotNull(frame);
                frame.Save(path);
            }
            finally { window.Close(); }
        // Continue off the UI dispatcher so session disposal cannot wait on its own worker thread.
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
        TestContext.AddResultFile(path);
    }

    [TestMethod]
    public async Task UnsafeLinksImagesAndComplexInputDoNotCreateActiveControls()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
        await session.Dispatch(() =>
        {
            var view = new ChatMarkdownView("[로컬](http://localhost/) ![이미지](https://tracker.example/pixel) `https://code.example/`", _ => Assert.Fail());
            var window = new Window { Content = view };
            try
            {
                window.Show(); Dispatcher.UIThread.RunJobs();
                Assert.HasCount(0, view.GetVisualDescendants().OfType<Button>().ToArray());
                Assert.HasCount(0, view.GetVisualDescendants().OfType<Image>().ToArray());
                var fallback = new ChatMarkdownView(new string('x', 65537), _ => Assert.Fail());
                Assert.AreEqual(65536, ((SelectableTextBlock)fallback.Children.Single()).Text!.Length);
                _ = new ChatMarkdownView(string.Concat(Enumerable.Repeat("> ", 100)) + "deep", _ => Assert.Fail());
            }
            finally { window.Close(); }
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }
}
