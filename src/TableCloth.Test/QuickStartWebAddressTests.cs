using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TableCloth.Components;
using TableCloth.Dialogs;
using TableCloth.Models.Catalog;
using TableCloth.Pages;
using TableCloth.Resources;
using TableCloth.ViewModels;

namespace TableCloth.Test;

[TestClass, DoNotParallelize]
public sealed class QuickStartWebAddressTests
{
    [TestMethod]
    public async Task CatalogLinkOpensAsModalChildOfMainWindow()
    {
        using var headless = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
        await headless.Dispatch<bool>(async () =>
        {
            var catalog = new CatalogWindow();
            var previous = TableClothApplication.ServiceProvider;
            TableClothApplication.ServiceProvider = new CatalogWindowProvider(catalog);
            var page = new QuickStartPage();
            var owner = new Window { Content = page, Width = 800, Height = 600 };
            try
            {
                owner.Show();
                var dialogTask = page.OpenCatalogAsync(null);
                for (var i = 0; i < 100 && !catalog.IsVisible; i++) await Task.Delay(10);
                Assert.IsTrue(catalog.IsVisible);
                Assert.AreSame(owner, catalog.Owner);
                Assert.IsFalse(dialogTask.IsCompleted);
                catalog.Close();
                await dialogTask;
            }
            finally
            {
                catalog.Close();
                owner.Close();
                TableClothApplication.ServiceProvider = previous;
            }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task ArrowKeysNavigateSuggestionsAndEnterAcceptsFocusedItem()
    {
        using var headless = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
        await headless.Dispatch<bool>(async () =>
        {
            Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
            var launcher = new Launcher();
            var services = new[]
            {
                new CatalogInternetService { Id = "bank-a", DisplayName = "국민은행", Url = "https://a.example/" },
                new CatalogInternetService { Id = "bank-b", DisplayName = "신한은행", Url = "https://b.example/" },
                new CatalogInternetService { Id = "bank-c", DisplayName = "하나은행", Url = "https://c.example/" },
                new CatalogInternetService { Id = "bank-d", DisplayName = "우리은행", Url = "https://d.example/" },
                new CatalogInternetService { Id = "bank-e", DisplayName = "기업은행", Url = "https://e.example/" },
            };
            var viewModel = new QuickStartPageViewModel(
                null!, null!, null!, null!, null!, new TaskFactory(), launcher, new CatalogCache(services));
            var page = new QuickStartPage { DataContext = viewModel };
            var window = new Window { Width = 800, Height = 600, Content = page };
            try
            {
                window.Show();
                await Task.Yield();
                var input = page.FindControl<TextBox>("StartInput")!;
                var suggestions = page.FindControl<Border>("StartSuggestions")!;
                input.Text = "은행";
                await Task.Yield();
                Assert.HasCount(5, viewModel.Suggestions);
                input.Focus();
                RaiseKey(input, Key.Down);
                var buttons = suggestions.GetVisualDescendants().OfType<Button>()
                    .Where(button => button.Classes.Contains("start-suggestion")).ToArray();
                Assert.HasCount(5, buttons);
                Assert.IsTrue(buttons[0].IsFocused);
                RaiseKey(buttons[0], Key.Down);
                Assert.IsTrue(buttons[1].IsFocused);
                using (var frame = window.CaptureRenderedFrame())
                {
                    Assert.IsNotNull(frame);
                    var screenshot = Path.Combine(AppContext.BaseDirectory,
                        "rendered-test-artifacts", "quickstart-suggestion-focused-dark-800.png");
                    Directory.CreateDirectory(Path.GetDirectoryName(screenshot)!);
                    frame.Save(screenshot);
                }
                Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
                await Task.Yield();
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using (var frame = window.CaptureRenderedFrame())
                {
                    Assert.IsNotNull(frame);
                    var screenshot = Path.Combine(AppContext.BaseDirectory,
                        "rendered-test-artifacts", "quickstart-suggestion-focused-800.png");
                    frame.Save(screenshot);
                }
                var selected = viewModel.Suggestions[1].Url;
                RaiseKey(buttons[1], Key.Enter);
                Assert.AreEqual(selected, viewModel.StartInput);
                Assert.IsFalse(viewModel.HasSuggestions);
                Assert.IsTrue(input.IsFocused);
                Assert.IsNull(launcher.Address);

                input.Text = "은행";
                await Task.Yield();
                input.Focus();
                RaiseKey(input, Key.Up);
                buttons = suggestions.GetVisualDescendants().OfType<Button>()
                    .Where(button => button.Classes.Contains("start-suggestion")).ToArray();
                Assert.IsTrue(buttons[^1].IsFocused);
                Assert.IsGreaterThan(0d, suggestions.GetVisualDescendants().OfType<ScrollViewer>().First().Offset.Y);
                selected = viewModel.Suggestions[^1].Url;
                RaiseKey(buttons[^1], Key.Enter);
                Assert.AreEqual(selected, viewModel.StartInput);
                Assert.IsFalse(viewModel.HasSuggestions);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    [TestMethod]
    public async Task SuggestionClickCompletesCatalogUrlBeforeStart()
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
                null!, null!, null!, null!, null!, new TaskFactory(), launcher, new CatalogCache(service))
            {
                NpkiStatusText = UIStringResources.QuickStart_NpkiStatus_Sharing,
            };
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
                Assert.AreEqual(service.Url, viewModel.StartInput);
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
    public async Task AddressInputDisablesStartUntilLaunchCompletes()
    {
        using var headless = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
        await headless.Dispatch<bool>(async () =>
        {
            var launcher = new Launcher
            {
                PendingResult = new TaskCompletionSource<InternetAddressLaunchResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously),
            };
            var viewModel = new QuickStartPageViewModel(
                null!, null!, null!, null!, null!, new TaskFactory(), launcher, null!)
            {
                NpkiStatusText = UIStringResources.QuickStart_NpkiStatus_Sharing,
            };
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
                Assert.IsFalse(button.IsEnabled);
                Assert.IsFalse(input.IsEnabled);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.AreEqual(1, launcher.LaunchCount);

                launcher.PendingResult.SetResult(InternetAddressLaunchResult.CatalogServiceLaunched);
                for (var i = 0; i < 100 && !button.IsEnabled; i++) await Task.Delay(10);
                Assert.IsTrue(button.IsEnabled);
                Assert.IsTrue(input.IsEnabled);
                Assert.IsFalse(viewModel.IsOpeningWebAddress);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    private sealed class Launcher : IInternetAddressSandboxLauncher
    {
        public string? Address;
        public int LaunchCount;
        public TaskCompletionSource<InternetAddressLaunchResult>? PendingResult;
        public Task<InternetAddressLaunchResult> LaunchAsync(string address,
            CancellationToken cancellationToken = default)
        {
            Address = address;
            LaunchCount++;
            return PendingResult?.Task ?? Task.FromResult(InternetAddressLaunchResult.CatalogServiceLaunched);
        }
    }

    private sealed class CatalogWindowProvider(CatalogWindow window) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(CatalogWindow) ? window : null;
    }

    private static void RaiseKey(Control control, Key key)
        => control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });

    private sealed class CatalogCache(params CatalogInternetService[] services) : IResourceCacheManager
    {
        public CatalogDocument CatalogDocument { get; } = new() { Services = services.ToList() };
        public IImage? GetImage(string siteId) => null;
        public Task<CatalogDocument> LoadCatalogDocumentAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CatalogDocument);
        public Task LoadSiteImagesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
