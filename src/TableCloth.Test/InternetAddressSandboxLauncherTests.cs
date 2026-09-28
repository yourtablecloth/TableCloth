using Avalonia.Media;
using TableCloth.Components;
using TableCloth.Components.Implementations;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Models.WindowsSandbox;
using System.Xml.Linq;

namespace TableCloth.Test;

[TestClass]
public sealed class InternetAddressSandboxLauncherTests
{
    [TestMethod]
    public async Task CatalogMatchPassesOriginalUrlAndServiceToQuickStartLaunch()
    {
        var service = Service("bank", "https://bank.example/");
        service.Packages.Add(new() { Name = "Required", Url = "https://bank.example/setup.exe" });
        var catalog = new Cache([service]);
        var choice = new Choice();
        var serviceLauncher = new ServiceLauncher();
        var browser = new BrowserLauncher();
        const string url = "https://secure.bank.example/product?q=%7e%2f&x=1";

        var result = await new InternetAddressSandboxLauncher(catalog, choice, serviceLauncher, browser)
            .LaunchAsync(url);

        Assert.AreEqual(InternetAddressLaunchResult.CatalogServiceLaunched, result);
        Assert.AreSame(service, serviceLauncher.Services!.Single());
        Assert.AreEqual(url, serviceLauncher.TargetUrl);
        Assert.IsNull(browser.Configuration);
        Assert.AreEqual(0, choice.Calls);
    }

    [TestMethod]
    public async Task UnmatchedPublicAddressUsesBrowserOnlySandbox()
    {
        var catalog = new Cache([Service("bank", "https://bank.example/")]);
        var serviceLauncher = new ServiceLauncher();
        var browser = new BrowserLauncher();
        const string url = "https://unlisted.example/path?q=%27%3B%24%28x%29";

        var result = await new InternetAddressSandboxLauncher(catalog, new Choice(), serviceLauncher, browser)
            .LaunchAsync(url);

        Assert.AreEqual(InternetAddressLaunchResult.BrowserOnlyLaunched, result);
        Assert.AreEqual(url, browser.Configuration!.BrowserOnlyUrl);
        Assert.IsEmpty(browser.Configuration.Services);
        Assert.IsEmpty(browser.Configuration.MappedFolders);
        Assert.IsNull(serviceLauncher.Services);
    }

    [TestMethod]
    [DataRow("https://127.0.0.1/")]
    [DataRow("http://10.0.0.1/")]
    [DataRow("https://bank.example@evil.example/")]
    [DataRow("javascript:alert(1)")]
    public async Task UnsafeAddressStartsNothing(string address)
    {
        var catalog = new Cache([]);
        var serviceLauncher = new ServiceLauncher();
        var browser = new BrowserLauncher();

        var result = await new InternetAddressSandboxLauncher(catalog, new Choice(), serviceLauncher, browser)
            .LaunchAsync(address);

        Assert.AreEqual(InternetAddressLaunchResult.InvalidAddress, result);
        Assert.AreEqual(0, catalog.Reads);
        Assert.IsNull(serviceLauncher.Services);
        Assert.IsNull(browser.Configuration);
    }

    [TestMethod]
    public async Task AmbiguousCatalogDomainRequiresSelection()
    {
        var services = Enumerable.Range(1, 4)
            .Select(index => Service("bank" + index, $"https://bank{index}.fsb.or.kr/"))
            .ToArray();
        var choice = new Choice { SelectedId = services[2].Id };
        var serviceLauncher = new ServiceLauncher();
        var browser = new BrowserLauncher();
        var launcher = new InternetAddressSandboxLauncher(new Cache(services), choice, serviceLauncher, browser);

        var result = await launcher.LaunchAsync("https://other.fsb.or.kr/page");

        Assert.AreEqual(InternetAddressLaunchResult.CatalogServiceLaunched, result);
        Assert.AreEqual(1, choice.Calls);
        Assert.HasCount(4, choice.Candidates!);
        Assert.AreSame(services[2], serviceLauncher.Services!.Single());
        Assert.IsNull(browser.Configuration);
    }

    [TestMethod]
    public async Task CancelingAmbiguousSelectionStartsNothing()
    {
        var services = Enumerable.Range(1, 4)
            .Select(index => Service("bank" + index, $"https://bank{index}.fsb.or.kr/"))
            .ToArray();
        var serviceLauncher = new ServiceLauncher();
        var browser = new BrowserLauncher();

        var result = await new InternetAddressSandboxLauncher(new Cache(services), new Choice(), serviceLauncher, browser)
            .LaunchAsync("https://other.fsb.or.kr/page");

        Assert.AreEqual(InternetAddressLaunchResult.Canceled, result);
        Assert.IsNull(serviceLauncher.Services);
        Assert.IsNull(browser.Configuration);
    }

    [TestMethod]
    public async Task AmbiguousChoiceCannotInjectUnrelatedService()
    {
        var services = Enumerable.Range(1, 4)
            .Select(index => Service("bank" + index, $"https://bank{index}.fsb.or.kr/"))
            .ToArray();
        var serviceLauncher = new ServiceLauncher();
        var browser = new BrowserLauncher();

        var result = await new InternetAddressSandboxLauncher(
            new Cache(services), new Choice { SelectedId = "unrelated" }, serviceLauncher, browser)
            .LaunchAsync("https://other.fsb.or.kr/page");

        Assert.AreEqual(InternetAddressLaunchResult.LaunchFailed, result);
        Assert.IsNull(serviceLauncher.Services);
        Assert.IsNull(browser.Configuration);
    }

    [TestMethod]
    public async Task MissingCatalogNeverFallsBackToUnpreparedBrowser()
    {
        var catalog = new Cache([]) { Fail = true };
        var serviceLauncher = new ServiceLauncher();
        var browser = new BrowserLauncher();

        var result = await new InternetAddressSandboxLauncher(catalog, new Choice(), serviceLauncher, browser)
            .LaunchAsync("https://unknown.example/");

        Assert.AreEqual(InternetAddressLaunchResult.CatalogUnavailable, result);
        Assert.IsNull(serviceLauncher.Services);
        Assert.IsNull(browser.Configuration);
    }

    [TestMethod]
    public async Task BrowserOnlyConfigurationGeneratesIsolatedSandboxSpec()
    {
        var directory = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory,
            "browser-only-" + Guid.NewGuid().ToString("N")));
        try
        {
            const string address = "https://unlisted.example/path?q=%27%3B%24%28x%29";
            var builder = new SandboxBuilder(null!, null!, null!, null!);
            var path = await builder.GenerateSandboxConfigurationAsync(directory.FullName,
                new TableClothConfiguration { BrowserOnlyUrl = address }, new List<SandboxMappedFolder>());

            Assert.AreEqual("BrowserOnly.wsb", Path.GetFileName(path));
            var root = XDocument.Load(path!).Root!;
            Assert.IsNull(root.Element("MappedFolders"));
            Assert.AreEqual("Disable", root.Element("ClipboardRedirection")?.Value);
            Assert.Contains("-EncodedCommand", root.Element("LogonCommand")?.Element("Command")?.Value!);
            Assert.DoesNotContain(address, File.ReadAllText(path!));
        }
        finally { directory.Delete(true); }
    }

    private static CatalogInternetService Service(string id, string url)
        => new() { Id = id, DisplayName = id, Url = url };

    private sealed class Cache(CatalogInternetService[] services) : IResourceCacheManager
    {
        public bool Fail;
        public int Reads;
        public CatalogDocument CatalogDocument
        {
            get
            {
                Reads++;
                if (Fail) throw new ArgumentNullException(nameof(CatalogDocument));
                return new() { Services = services.ToList() };
            }
        }
        public IImage? GetImage(string siteId) => null;
        public Task<CatalogDocument> LoadCatalogDocumentAsync(CancellationToken cancellationToken = default)
            => Fail ? throw new IOException("Catalog unavailable") : Task.FromResult(CatalogDocument);
        public Task LoadSiteImagesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Choice : ICatalogServiceChoice
    {
        public string? SelectedId;
        public int Calls;
        public IReadOnlyList<CatalogInternetService>? Candidates;
        public Task<string?> SelectAsync(Uri target, IReadOnlyList<CatalogInternetService> candidates,
            CancellationToken cancellationToken)
        {
            Calls++;
            Candidates = candidates;
            return Task.FromResult(SelectedId);
        }
    }

    private sealed class ServiceLauncher : ICatalogServiceLauncher
    {
        public IReadOnlyList<CatalogInternetService>? Services;
        public string? TargetUrl;
        public Task<bool> LaunchAsync(IReadOnlyList<CatalogInternetService> services, string targetUrl,
            CancellationToken cancellationToken)
        {
            Services = services;
            TargetUrl = targetUrl;
            return Task.FromResult(true);
        }
    }

    private sealed class BrowserLauncher : ISandboxLauncher
    {
        public TableClothConfiguration? Configuration;
        public Task<bool> RunSandboxAsync(TableClothConfiguration config, CancellationToken cancellationToken = default)
        {
            Configuration = config;
            return Task.FromResult(true);
        }
        public bool ValidateSandboxSpecFile(string wsbFilePath, out string? reason)
            => throw new NotSupportedException();
    }
}
