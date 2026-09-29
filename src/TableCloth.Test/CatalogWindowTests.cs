using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.VisualTree;
using TableCloth.Components;
using TableCloth.Dialogs;
using TableCloth.Models;
using TableCloth.Models.Catalog;
using TableCloth.ViewModels;

namespace TableCloth.Test;

[TestClass, DoNotParallelize]
public sealed class CatalogWindowTests
{
    [TestMethod]
    public async Task SearchAndSelectionPassCatalogServiceToQuickStartLaunch()
    {
        using var headless = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
        await headless.Dispatch<bool>(async () =>
        {
            var alpha = new CatalogInternetService { Id = "alpha", DisplayName = "Alpha Bank", Url = "https://alpha.example/" };
            var beta = new CatalogInternetService { Id = "beta", DisplayName = "Beta Bank", Url = "https://beta.example/" };
            var navigation = new Navigation();
            var viewModel = new CatalogWindowViewModel(new CatalogCache(alpha, beta), navigation);
            var window = new CatalogWindow(viewModel);
            try
            {
                Assert.AreEqual(WindowStartupLocation.CenterOwner, window.WindowStartupLocation);
                window.Show();
                await Task.Yield();
                var screenshot = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts", "catalog-list-800-light.png");
                Directory.CreateDirectory(Path.GetDirectoryName(screenshot)!);
                using (var frame = window.CaptureRenderedFrame())
                {
                    Assert.IsNotNull(frame);
                    frame.Save(screenshot);
                }
                var list = window.GetVisualDescendants().OfType<ListBox>().Single();
                Assert.HasCount(2, viewModel.FilteredServices);

                viewModel.SearchKeyword = "beta";
                Assert.HasCount(1, viewModel.FilteredServices);
                Assert.AreSame(beta, viewModel.FilteredServices[0]);

                viewModel.SearchByName("beta");
                Assert.HasCount(1, viewModel.FilteredServices);
                viewModel.ShowAll();
                Assert.HasCount(2, viewModel.FilteredServices);
                viewModel.SearchByName("beta.example");
                Assert.HasCount(0, viewModel.FilteredServices);
                viewModel.SearchByName("beta");

                list.SelectedItem = beta;
                viewModel.LaunchSelectedCommand.Execute(null);
                Assert.AreSame(beta, navigation.SelectedService);
                Assert.IsNull(navigation.TargetUrl);
                Assert.IsFalse(window.IsVisible);
            }
            finally { window.Close(); }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    private sealed class CatalogCache(params CatalogInternetService[] services) : IResourceCacheManager
    {
        public CatalogDocument CatalogDocument { get; } = new() { Services = services.ToList() };
        public IImage? GetImage(string siteId) => null;
        public Task<CatalogDocument> LoadCatalogDocumentAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CatalogDocument);
        public Task LoadSiteImagesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Navigation : INavigationService
    {
        public CatalogInternetService? SelectedService { get; private set; }
        public string? TargetUrl { get; private set; }
        public bool NavigateToCatalog(string searchKeyword) => throw new NotSupportedException();
        public bool NavigateToDetail(string searchKeyword, CatalogInternetService selectedService, CommandLineArgumentModel? arguments)
            => throw new NotSupportedException();
        public bool NavigateToQuickStart() => throw new NotSupportedException();
        public bool NavigateToQuickStartAndLaunch(IEnumerable<CatalogInternetService> services, string? targetUrl)
        {
            SelectedService = services.Single();
            TargetUrl = targetUrl;
            return true;
        }
        public void GoBack() => throw new NotSupportedException();
    }
}
