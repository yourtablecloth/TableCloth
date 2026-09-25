using TableCloth.Components;
using TableCloth.ManagedAi;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using Avalonia.Media;
using TableCloth.Components.Implementations;
using TableCloth.ViewModels;
using TableCloth.Models;
using System.Reflection;

namespace TableCloth.Test;

[TestClass]
public sealed class ManagedAiBrowserAdapterTests
{
    [TestMethod]
    public async Task CurrentBrowserChoiceSkipsCatalogAndSandbox()
    {
        var sandbox = new RecordingLauncher();
        var catalog = new CatalogLauncher();
        var cache = new CatalogCache(new() { Services = [Service("Bank", "https://bank.example/")] }) { FailLoad = true };
        var openChoice = new LinkChoice { Selected = ManagedAiLinkOpenDestination.CurrentBrowser };
        var host = new HostBrowser();
        const string url = "https://bank.example/product?q=%7e%2f&x=1#section";
        await new TableClothBrowserAdapter(sandbox, cache, catalog, new Choice(), openChoice, host)
            .OpenAsync(new(url), CancellationToken.None);
        Assert.AreEqual(url, host.Target!.OriginalString);
        Assert.AreEqual(url, openChoice.Target!.OriginalString);
        Assert.AreEqual(0, cache.Loads);
        Assert.IsNull(sandbox.Configuration);
        Assert.IsNull(catalog.Services);
    }

    [TestMethod]
    public async Task CancelChoiceLaunchesNothing()
    {
        var sandbox = new RecordingLauncher();
        var catalog = new CatalogLauncher();
        var cache = new CatalogCache(new()) { Missing = true, FailLoad = true };
        var host = new HostBrowser();
        await new TableClothBrowserAdapter(sandbox, cache, catalog, new Choice(),
            new LinkChoice { Selected = ManagedAiLinkOpenDestination.Cancel }, host)
            .OpenAsync(new("https://example.com/"), CancellationToken.None);
        Assert.AreEqual(0, cache.Loads);
        Assert.IsNull(host.Target);
        Assert.IsNull(sandbox.Configuration);
        Assert.IsNull(catalog.Services);
    }

    [TestMethod]
    public async Task SandboxChoiceRetainsCatalogAndSporkFlow()
    {
        var service = Service("Bank", "https://bank.example/");
        service.Packages.Add(new() { Name = "RequiredPackage", Url = "https://bank.example/setup.exe" });
        var catalog = new CatalogLauncher();
        var host = new HostBrowser();
        await new TableClothBrowserAdapter(new RecordingLauncher(), new CatalogCache(new() { Services = [service] }),
            catalog, new Choice(), new LinkChoice { Selected = ManagedAiLinkOpenDestination.WindowsSandbox }, host)
            .OpenAsync(new("https://bank.example/product"), CancellationToken.None);
        Assert.AreSame(service, catalog.Services!.Single());
        Assert.IsNull(host.Target);
    }

    [TestMethod]
    [DataRow("https://bank.example/product")]
    [DataRow("http://bank.example/product")]
    public async Task ExplicitSelectionUsesExistingLauncherWithoutCatalogOrHostMappings(string url)
    {
        var launcher = new RecordingLauncher();
        await SandboxAdapter(launcher, new CatalogCache(new()), new CatalogLauncher(), new Choice())
            .OpenAsync(new Uri(url), CancellationToken.None);
        Assert.IsNotNull(launcher.Configuration);
        Assert.AreEqual(url, launcher.Configuration.ManagedAiBrowserOnlyUrl);
        Assert.IsNull(launcher.Configuration.TargetUrl);
        Assert.HasCount(0, launcher.Configuration.Services);
        Assert.HasCount(0, launcher.Configuration.MappedFolders);
    }

    [TestMethod]
    public async Task UnsafeSelectionNeverReachesLauncher()
    {
        var launcher = new RecordingLauncher();
        await Assert.ThrowsExactlyAsync<ManagedAiException>(() => SandboxAdapter(launcher, new CatalogCache(new()), new CatalogLauncher(), new Choice())
            .OpenAsync(new Uri("https://127.0.0.1/"), CancellationToken.None));
        Assert.IsNull(launcher.Configuration);
    }

    [TestMethod]
    [DataRow("https://secure.bank.example/account?q=%7e%2f&x=1#section")]
    [DataRow("http://secure.bank.example/account")]
    public async Task CatalogMatchUsesSporkFlowAndPreservesOriginalDestination(string url)
    {
        var catalog = new CatalogDocument { Services = [Service("Personal", "https://www.bank.example/")] };
        catalog.Services[0].Packages.Add(new() { Name = "RequiredPackage", Url = "https://cdn.bank.example/setup.exe" });
        var browser = new RecordingLauncher();
        var launcher = new CatalogLauncher();
        var choice = new Choice();
        await SandboxAdapter(browser, new CatalogCache(catalog), launcher, choice).OpenAsync(new(url), CancellationToken.None);
        Assert.IsNull(browser.Configuration);
        Assert.AreSame(catalog.Services[0], launcher.Services!.Single());
        Assert.AreEqual("RequiredPackage", launcher.Services![0].Packages.Single().Name);
        Assert.AreEqual(url, launcher.Url);
        Assert.AreEqual(0, choice.Calls);
    }

    [TestMethod]
    public async Task MoreSpecificHostSelectsBusinessService()
    {
        var catalog = new CatalogDocument { Services = [Service("Personal", "https://www.bank.example/"), Service("Business", "https://biz.bank.example/")] };
        var launcher = new CatalogLauncher();
        await SandboxAdapter(new RecordingLauncher(), new CatalogCache(catalog), launcher, new Choice())
            .OpenAsync(new("https://biz.bank.example/finance"), CancellationToken.None);
        Assert.AreEqual("Business", launcher.Services!.Single().Id);
    }

    [TestMethod]
    [DataRow("https://bank.example.attacker.com/")]
    [DataRow("https://fakebank.example/")]
    [DataRow("https://elsewhere.co.kr/")]
    public async Task LookalikeDomainDoesNotInstallCatalogSoftware(string url)
    {
        var catalog = new CatalogDocument { Services = [Service("Bank", "https://bank.example/"), Service("Korean", "https://bank.co.kr/")] };
        var browser = new RecordingLauncher();
        var launcher = new CatalogLauncher();
        await SandboxAdapter(browser, new CatalogCache(catalog), launcher, new Choice()).OpenAsync(new(url), CancellationToken.None);
        Assert.IsNotNull(browser.Configuration);
        Assert.IsNull(launcher.Services);
    }

    [TestMethod]
    [DataRow("Bank2")]
    [DataRow(null)]
    [DataRow("Unrelated")]
    public async Task AmbiguousCatalogMatchRequiresAValidSelection(string? selected)
    {
        var catalog = new CatalogDocument { Services = Enumerable.Range(1, 4).Select(i => Service("Bank" + i, $"https://bank{i}.shared.example/")).ToList() };
        var browser = new RecordingLauncher();
        var launcher = new CatalogLauncher();
        var choice = new Choice { Selected = selected };
        var adapter = SandboxAdapter(browser, new CatalogCache(catalog), launcher, choice);
        if (selected == "Bank2")
        {
            await adapter.OpenAsync(new("https://unknown.shared.example/"), CancellationToken.None);
            Assert.AreEqual("Bank2", launcher.Services!.Single().Id);
        }
        else if (selected is null)
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => adapter.OpenAsync(new("https://unknown.shared.example/"), CancellationToken.None));
        else
            await Assert.ThrowsExactlyAsync<ManagedAiException>(() => adapter.OpenAsync(new("https://unknown.shared.example/"), CancellationToken.None));
        Assert.AreEqual(1, choice.Calls);
        Assert.HasCount(4, choice.Candidates!);
        Assert.IsNull(browser.Configuration);
        if (selected != "Bank2") Assert.IsNull(launcher.Services);
    }

    [TestMethod]
    public async Task CatalogLoadFailureDoesNotSilentlySkipRequiredSoftware()
    {
        var browser = new RecordingLauncher();
        var launcher = new CatalogLauncher();
        var cache = new CatalogCache(new()) { Missing = true, FailLoad = true };
        var error = await Assert.ThrowsExactlyAsync<ManagedAiException>(() => SandboxAdapter(browser, cache, launcher, new Choice())
            .OpenAsync(new("https://bank.example/"), CancellationToken.None));
        Assert.AreEqual(AiFailureCode.CatalogUnavailable, error.Code);
        Assert.IsNull(browser.Configuration);
        Assert.IsNull(launcher.Services);
    }

    [TestMethod]
    public async Task MissingCatalogIsLoadedAndFailedLaunchIsReported()
    {
        var cache = new CatalogCache(new() { Services = [Service("Bank", "https://bank.example/")] }) { Missing = true };
        var launcher = new CatalogLauncher { Success = false };
        var error = await Assert.ThrowsExactlyAsync<ManagedAiException>(() => SandboxAdapter(new RecordingLauncher(), cache, launcher, new Choice())
            .OpenAsync(new("https://bank.example/"), CancellationToken.None));
        Assert.AreEqual(1, cache.Loads);
        Assert.AreEqual(AiFailureCode.BrowserOpenFailed, error.Code);
    }

    [TestMethod]
    public async Task CancellationPreventsAnyLaunch()
    {
        var browser = new RecordingLauncher();
        var launcher = new CatalogLauncher();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => SandboxAdapter(browser, new CatalogCache(new()), launcher, new Choice())
            .OpenAsync(new("https://bank.example/"), new CancellationToken(true)));
        Assert.IsNull(browser.Configuration);
        Assert.IsNull(launcher.Services);
    }

    [TestMethod]
    public async Task CatalogLinkReachesQuickStartConfigurationAndSporkInstallSteps()
    {
        var directory = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "catalog-flow-" + Guid.NewGuid().ToString("N")));
        try
        {
            var service = Service("ExampleBank", "https://bank.example/");
            service.Packages.Add(new() { Name = "BankSecurity", Url = "https://bank.example/setup.exe", Arguments = "/quiet" });
            var cache = new CatalogCache(new() { Services = [service] });
            var sandbox = new RecordingLauncher();
            var preferences = new Preferences(new()
            {
                DataDirectoryHostPath = directory.FullName, LastDisclaimerAgreedTime = DateTime.UtcNow,
                ShareNpkiFolder = false, EnableSandboxGpuAcceleration = true
            });
            var provider = new ViewModelProvider();
            var ui = new AppUserInterface(provider, cache, null!);
            provider.ViewModel = new QuickStartPageViewModel(preferences, ui, new SharedLocations(), sandbox, null!, new TaskFactory());
            const string url = "https://secure.bank.example/product?q=%7e%2f&x=1";
            await SandboxAdapter(sandbox, cache, new ManagedAiCatalogLauncher(ui), new Choice()).OpenAsync(new(url), CancellationToken.None);
            var config = sandbox.Configuration!;
            Assert.IsNull(config.ManagedAiBrowserOnlyUrl);
            Assert.AreEqual(url, config.TargetUrl);
            Assert.AreSame(service, config.Services.Single());
            Assert.IsTrue(config.EnableSandboxGpuAcceleration);
            Assert.AreEqual(directory.FullName, config.MappedFolders.Single().HostFolder);

            // Inspect, but do not execute, the guest startup command and Spork installation plan.
            var method = typeof(SandboxBuilder).GetMethod("GenerateSandboxStartupScript", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var script = (string)method.Invoke(new SandboxBuilder(null!, null!, null!, null!), [config])!;
            Assert.Contains("spork ExampleBank", script);
            Assert.Contains("--target-url", script);
            Assert.Contains("?q=%%7e%%2f&x=1", script);
            var stepFactory = new StepFactory();
            var steps = new Spork.Steps.Implementations.StepsComposer(new TaskFactory(), stepFactory, new Arguments(), cache, null!)
                .ComposeStepsForSites(config.Services.Select(x => x.Id), forceReinstall: true).ToArray();
            var package = steps.Single(x => x.PackageName == "BankSecurity");
            var argument = (Spork.ViewModels.PackageInstallItemViewModel)package.Argument;
            Assert.AreEqual(service.Packages[0].Url, argument.PackageUrl);
            Assert.AreEqual("/quiet", argument.Arguments);
            Assert.Contains("PackageInstallStep", stepFactory.Names);
        }
        finally { directory.Delete(); }
    }

    private sealed class ViewModelProvider : IServiceProvider
    {
        public QuickStartPageViewModel ViewModel = null!;
        public object? GetService(Type serviceType) => serviceType == typeof(QuickStartPageViewModel) ? ViewModel : null;
    }

    private static TableClothBrowserAdapter SandboxAdapter(ISandboxLauncher sandbox, IResourceCacheManager resources,
        IManagedAiCatalogLauncher catalog, IManagedAiCatalogChoice catalogChoice)
        => new(sandbox, resources, catalog, catalogChoice,
            new LinkChoice { Selected = ManagedAiLinkOpenDestination.WindowsSandbox }, new HostBrowser());
    private sealed class Preferences(PreferenceSettings value) : IPreferencesManager
    {
        public PreferenceSettings GetDefaultPreferences() => value;
        public Task<PreferenceSettings?> LoadPreferencesAsync(CancellationToken cancellationToken = default) => Task.FromResult<PreferenceSettings?>(value);
        public Task SavePreferencesAsync(PreferenceSettings preferences, CancellationToken cancellationToken = default) => throw new AssertFailedException("Unexpected preference write");
    }
    private sealed class Arguments : Spork.Components.ICommandLineArguments
    {
        public CommandLineArgumentModel GetCurrent() => new([]);
        public Task<string> GetHelpStringAsync() => throw new NotSupportedException();
        public Task<string> GetVersionStringAsync() => throw new NotSupportedException();
    }
    private sealed class StepFactory : Spork.Steps.IStepsFactory
    {
        public List<string> Names = [];
        public Spork.Steps.IStep GetStepByName(string name) { Names.Add(name); return null!; }
    }

    private static CatalogInternetService Service(string id, string url) => new() { Id = id, DisplayName = id, Url = url };
    private sealed class CatalogCache(CatalogDocument catalog) : IResourceCacheManager, Spork.Components.IResourceCacheManager
    {
        public bool Missing, FailLoad;
        public int Loads;
        public CatalogDocument CatalogDocument => Missing ? throw new ArgumentNullException(nameof(catalog)) : catalog;
        public IImage? GetImage(string siteId) => null;
        public Task LoadSiteImagesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<CatalogDocument> LoadCatalogDocumentAsync(CancellationToken cancellationToken = default)
        {
            Loads++;
            if (FailLoad) throw new IOException("fixture unavailable");
            Missing = false;
            return Task.FromResult(catalog);
        }
    }
    private sealed class CatalogLauncher : IManagedAiCatalogLauncher
    {
        public IReadOnlyList<CatalogInternetService>? Services;
        public string? Url;
        public bool Success = true;
        public Task<bool> LaunchAsync(IReadOnlyList<CatalogInternetService> services, string targetUrl, CancellationToken cancellationToken)
        { Services = services; Url = targetUrl; return Task.FromResult(Success); }
    }
    private sealed class Choice : IManagedAiCatalogChoice
    {
        public int Calls;
        public string? Selected;
        public IReadOnlyList<CatalogInternetService>? Candidates;
        public Task<string?> SelectAsync(Uri target, IReadOnlyList<CatalogInternetService> candidates, CancellationToken cancellationToken)
        { Calls++; Candidates = candidates; return Task.FromResult(Selected); }
    }

    private sealed class LinkChoice : IManagedAiLinkOpenChoice
    {
        public ManagedAiLinkOpenDestination Selected;
        public Uri? Target;
        public Task<ManagedAiLinkOpenDestination> SelectAsync(Uri target, CancellationToken cancellationToken)
        { Target = target; return Task.FromResult(Selected); }
    }

    private sealed class HostBrowser : IManagedAiHostBrowser
    {
        public Uri? Target;
        public Task OpenAsync(Uri target, CancellationToken cancellationToken)
        { Target = target; return Task.CompletedTask; }
    }

    private sealed class RecordingLauncher : ISandboxLauncher
    {
        public TableClothConfiguration? Configuration;
        public Task<bool> RunSandboxAsync(TableClothConfiguration config, CancellationToken cancellationToken = default)
        { Configuration = config; return Task.FromResult(true); }
        public bool ValidateSandboxSpecFile(string path, out string? reason) { reason = null; return true; }
    }
}
