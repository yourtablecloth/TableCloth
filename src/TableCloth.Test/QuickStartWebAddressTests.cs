using Avalonia.Controls;
using Avalonia.Headless;
using TableCloth.Components;
using TableCloth.Pages;
using TableCloth.ViewModels;

namespace TableCloth.Test;

[TestClass, DoNotParallelize]
public sealed class QuickStartWebAddressTests
{
    [TestMethod]
    public async Task AddressInputShowsProgressAndLaunchResultWithoutAiWindow()
    {
        using var headless = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
        await headless.Dispatch<bool>(async () =>
        {
            var launcher = new Launcher();
            var viewModel = new QuickStartPageViewModel(
                null!, null!, null!, null!, null!, new TaskFactory(), launcher);
            var page = new QuickStartPage { DataContext = viewModel };
            var window = new Window { Width = 800, Height = 600, Content = page };
            try
            {
                window.Show();
                await Task.Yield();
                var input = page.FindControl<TextBox>("WebAddressInput")!;
                var button = page.FindControl<Button>("WebAddressLaunch")!;
                Assert.IsNotNull(button.Command);
                input.Text = "https://bank.example/path";
                Assert.AreEqual("https://bank.example/path", viewModel.WebAddress);

                var screenshot = Path.Combine(AppContext.BaseDirectory,
                    "rendered-test-artifacts", "quickstart-web-address-800.png");
                Directory.CreateDirectory(Path.GetDirectoryName(screenshot)!);
                using (var frame = window.CaptureRenderedFrame())
                {
                    Assert.IsNotNull(frame);
                    frame.Save(screenshot);
                }

                await viewModel.OpenWebAddressCommand.ExecuteAsync(null);
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
}
