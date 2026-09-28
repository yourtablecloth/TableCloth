using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using TableCloth.Components;
using TableCloth.Models.Catalog;
using TableCloth.Pages;
using TableCloth.ViewModels;

namespace TableCloth.Test;

[TestClass, DoNotParallelize]
public sealed class QuickStartWebAddressTests
{
    [TestMethod]
    public async Task SuggestionClickCompletesNameOrAddressBeforeStart()
    {
        using var headless = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
        await headless.Dispatch<bool>(async () =>
        {
            var launcher = new Launcher();
            var service = new CatalogInternetService
            {
                Id = "bank", DisplayName = "국민은행", Url = "https://bank.example/",
            };
            var viewModel = new QuickStartPageViewModel(
                null!, null!, null!, null!, null!, new TaskFactory(), launcher, new CatalogCache(service));
            var page = new QuickStartPage { DataContext = viewModel };
            var window = new Window { Width = 800, Height = 600, Content = page };
            try
            {
                window.Show();
                await Task.Yield();
                var input = page.FindControl<TextBox>("StartInput")!;
                var suggestions = page.FindControl<Border>("StartSuggestions")!;
                input.Text = "국민";
                Assert.IsTrue(viewModel.HasSuggestions);
                Assert.AreEqual(QuickStartSuggestionKind.CatalogName, viewModel.Suggestions[0].Kind);
                await Task.Yield();
                Assert.IsTrue(suggestions.IsVisible);
                using (var frame = window.CaptureRenderedFrame())
                {
                    Assert.IsNotNull(frame);
                    var screenshot = Path.Combine(AppContext.BaseDirectory,
                        "rendered-test-artifacts", "quickstart-suggestions-800.png");
                    Directory.CreateDirectory(Path.GetDirectoryName(screenshot)!);
                    frame.Save(screenshot);
                }
                var first = suggestions.GetVisualDescendants().OfType<Button>().First();
                first.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.AreEqual("국민은행", viewModel.StartInput);
                Assert.IsFalse(viewModel.HasSuggestions);

                input.Text = "https://bank.";
                Assert.IsTrue(viewModel.HasSuggestions);
                Assert.AreEqual(QuickStartSuggestionKind.WebAddress, viewModel.Suggestions[0].Kind);
                await Task.Yield();
                using (var frame = window.CaptureRenderedFrame()) Assert.IsNotNull(frame);
                first = suggestions.GetVisualDescendants().OfType<Button>().First();
                first.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.AreEqual(service.Url, viewModel.StartInput);
                Assert.IsNull(launcher.Address);

                page.FindControl<Button>("StartButton")!
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                for (var i = 0; i < 100 && launcher.Address is null; i++) await Task.Delay(10);
                Assert.AreEqual(service.Url, launcher.Address);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task AddressInputShowsProgressAndLaunchResultWithoutAiWindow()
    {
        using var headless = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
        await headless.Dispatch<bool>(async () =>
        {
            var launcher = new Launcher();
            var viewModel = new QuickStartPageViewModel(
                null!, null!, null!, null!, null!, new TaskFactory(), launcher, null!);
            var page = new QuickStartPage { DataContext = viewModel };
            var window = new Window { Width = 800, Height = 600, Content = page };
            try
            {
                window.Show();
                await Task.Yield();
                var input = page.FindControl<TextBox>("StartInput")!;
                var button = page.FindControl<Button>("StartButton")!;
                input.Text = "https://bank.example/path";
                Assert.AreEqual("https://bank.example/path", viewModel.StartInput);

                var screenshot = Path.Combine(AppContext.BaseDirectory,
                    "rendered-test-artifacts", "quickstart-web-address-800.png");
                Directory.CreateDirectory(Path.GetDirectoryName(screenshot)!);
                using (var frame = window.CaptureRenderedFrame())
                {
                    Assert.IsNotNull(frame);
                    frame.Save(screenshot);
                }

                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                for (var i = 0; i < 100 && launcher.Address is null; i++) await Task.Delay(10);
                Assert.AreEqual("https://bank.example/path", launcher.Address);
                Assert.Contains("Spork", viewModel.WebAddressStatusText);
                Assert.IsFalse(viewModel.IsOpeningWebAddress);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    private sealed class Launcher : IInternetAddressSandboxLauncher
    {
        public string? Address;
        public Task<InternetAddressLaunchResult> LaunchAsync(string address,
            CancellationToken cancellationToken = default)
        {
            Address = address;
            return Task.FromResult(InternetAddressLaunchResult.CatalogServiceLaunched);
        }
    }

    private sealed class CatalogCache(params CatalogInternetService[] services) : IResourceCacheManager
    {
        public CatalogDocument CatalogDocument { get; } = new() { Services = services.ToList() };
        public IImage? GetImage(string siteId) => null;
        public Task<CatalogDocument> LoadCatalogDocumentAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CatalogDocument);
        public Task LoadSiteImagesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
