using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
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
