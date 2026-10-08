using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using TableCloth.Components;
using TableCloth.Components.Implementations;
using TableCloth.Dialogs;
using TableCloth.Models.Catalog;
using TableCloth.Models.Configuration;
using TableCloth.Models.WindowsSandbox;
using TableCloth.Resources;
using TableCloth.Serialization;
using TableCloth.ViewModels;

namespace TableCloth.Test;

[TestClass, DoNotParallelize]
public sealed class SafeModeTests
{
    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public async Task SafeModeSkipsAllHostStagingEvenWhenEverySharingOptionIsEnabled(bool stored, bool requested)
    {
        var settings = AllSharingEnabled();
        settings.EnableSafeMode = stored;
        var preferences = new Preferences(settings);
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Safe Mode Test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(1));
        var config = new TableClothConfiguration
        {
            EnableSafeMode = requested,
            EnableMicrophone = true, EnableWebCam = true, EnablePrinters = true, EnableSandboxGpuAcceleration = true,
            HostDotnetRootPath = "C:\\host-runtime", AssetsDirectoryPath = "C:\\host-app",
            MappedFolders = settings.MappedFolders,
            CertPair = new X509CertPair(certificate.Export(X509ContentType.Cert), key.ExportPkcs8PrivateKey()),
        };
        var output = Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "safe-mode-" + Guid.NewGuid().ToString("N")));
        try
        {
            // Null collaborators deliberately fail if image expansion, install/cache copies or UI staging are reached.
            var builder = new SandboxBuilder(null!, null!, null!, preferences);
            var excluded = new List<SandboxMappedFolder>();
            var path = await builder.GenerateSandboxConfigurationAsync(output.FullName, config, excluded);
            Assert.AreEqual("SafeMode.wsb", Path.GetFileName(path));
            AssertPolicy(XDocument.Load(path!).Root!);
            Assert.HasCount(1, output.GetFileSystemInfos());
            if (requested)
            {
                var artifactDirectory = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts");
                Directory.CreateDirectory(artifactDirectory);
                File.Copy(path!, Path.Combine(artifactDirectory, "SafeMode.wsb"), true);
            }
            Assert.IsEmpty(excluded);
            Assert.IsTrue(config.EnableSafeMode);
            Assert.IsTrue(settings.ShareNpkiFolder);
            Assert.IsTrue(settings.EnableZScalerRootCertPropagation);
            Assert.IsTrue(settings.EnableSandboxGpuAcceleration);
            Assert.HasCount(1, settings.MappedFolders);
        }
        finally { output.Delete(true); }
    }

    [TestMethod]
    public void SafeModeIsOptInAndSurvivesPreferenceSerialization()
    {
        Assert.IsFalse(new PreferenceSettings().EnableSafeMode);
        var settings = System.Text.Json.JsonSerializer.Deserialize("{}", TableClothJsonContext.Default.PreferenceSettings)!;
        Assert.IsFalse(settings.EnableSafeMode);
        settings = AllSharingEnabled(); settings.EnableSafeMode = true;
        var json = System.Text.Json.JsonSerializer.Serialize(settings, TableClothJsonContext.Default.PreferenceSettings);
        var restored = System.Text.Json.JsonSerializer.Deserialize(json, TableClothJsonContext.Default.PreferenceSettings)!;
        Assert.IsTrue(restored.EnableSafeMode);
        Assert.IsTrue(restored.ShareNpkiFolder);
        Assert.HasCount(1, restored.MappedFolders);
    }

    [TestMethod]
    public void SiteSelectionAndUrlUseTheExistingCatalogAndQuoteContract()
    {
        var config = new TableClothConfiguration
        {
            Services = [new() { Id = "bank-a", Url = "https://www.example.com/" }, new() { Id = "bank-b", Url = "https://other.example.com/" }],
            TargetUrl = "https://www.example.com/path?q=';&next=$(`x)&a=1",
        };
        var spec = SafeModeSandboxSpec.Create(config, false);
        var root = XDocument.Parse(spec).Root!;
        AssertPolicy(root);
        var command = root.Element("LogonCommand")!.Element("Command")!.Value;
        Assert.Contains("bank-a bank-b", command);
        Assert.Contains("q=%27;&next=$(`x)&a=1", command);
        Assert.Contains("if ($false -and", command);
        Assert.Contains("guest executable=", command);
        Assert.DoesNotContain("__SAFE_", command);
        Assert.DoesNotContain("-EncodedCommand", command);
        Assert.Contains("&amp;", spec);
    }

    [TestMethod]
    [DataRow("https://evil.example.net/")]
    [DataRow("https://user@www.example.com/")]
    [DataRow("file:///C:/Windows/system.ini")]
    [DataRow("https://www.example.com/\";boom")]
    public void UntrustedTargetUrlsAreRejected(string target)
    {
        var config = new TableClothConfiguration { Services = [new() { Id = "bank", Url = "https://www.example.com/" }], TargetUrl = target };
        Assert.ThrowsExactly<ArgumentException>(() => SafeModeSandboxSpec.Create(config, true));
    }

    [TestMethod]
    [DataRow("bank';boom")]
    [DataRow("bank other")]
    [DataRow("--help")]
    public void UntrustedSiteIdsAreRejected(string id)
    {
        Assert.ThrowsExactly<ArgumentException>(() => SafeModeSandboxSpec.Create(
            new TableClothConfiguration { Services = [new() { Id = id }] }, true));
    }

    [TestMethod]
    public async Task DeepLinkSafeModeLaunchSkipsNonLocalDataValidationAndCertificateSharing()
    {
        var settings = AllSharingEnabled(); settings.EnableSafeMode = true;
        settings.LastDisclaimerAgreedTime = DateTime.UtcNow;
        var launcher = new Launcher();
        var locations = new Locations();
        // Data mount checks, directory creation/migration and NPKI lookup must not run in Safe Mode.
        var vm = new QuickStartPageViewModel(new Preferences(settings), null!, locations, launcher, null!,
            new TaskFactory(), null!, null!);
        var service = new CatalogInternetService { Id = "bank", Url = "https://www.example.com/" };
        Assert.IsTrue(await vm.LaunchForDeepLinkAsync([service], "https://www.example.com/path"));
        var config = launcher.Configuration!;
        Assert.IsTrue(config.EnableSafeMode);
        Assert.IsEmpty(config.MappedFolders);
        Assert.IsNull(config.CertPair);
        Assert.AreSame(service, config.Services.Single());
        Assert.AreEqual("https://www.example.com/path", config.TargetUrl);
        Assert.AreEqual(SafeModeText.Status, vm.ExecutionModeText);
        Assert.AreEqual(0, locations.MountChecks);
    }

    [TestMethod]
    [DataRow("ko-KR", "Safe Mode 사용")]
    [DataRow("en-US", "Use Safe Mode")]
    public async Task SessionTabShowsAndPersistsSafeModeWithoutChangingNormalOptions(string culture, string label)
    {
        using var headless = HeadlessUnitTestSession.StartNew(typeof(MarkdownTestAppBuilder));
        await headless.Dispatch<bool>(async () =>
        {
            var previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var preferences = new Preferences(AllSharingEnabled());
            var vm = new OptionsWindowViewModel(preferences, null!, null!, new Locations(), new TaskFactory(), null!);
            vm.SetInitialTab(OptionsTabKeys.Session);
            await vm.OptionsWindowLoadedCommand.ExecuteAsync(null);
            var window = new OptionsWindow { DataContext = vm, Width = 600, Height = 480 };
            try
            {
                window.Show(); await Task.Yield();
                var toggle = window.FindControl<CheckBox>("SafeModeToggle")!;
                Assert.AreEqual(label, toggle.Content);
                Assert.IsFalse(toggle.IsChecked ?? false);
                toggle.IsChecked = true;
                Assert.IsTrue(preferences.Value.EnableSafeMode);
                Assert.IsTrue(preferences.Value.UseAudioRedirection);
                Assert.IsTrue(preferences.Value.ShareNpkiFolder);
                Assert.IsTrue(preferences.Value.EnableSandboxGpuAcceleration);
                Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using var frame = window.CaptureRenderedFrame(); Assert.IsNotNull(frame);
                var directory = Path.Combine(AppContext.BaseDirectory, "rendered-test-artifacts");
                Directory.CreateDirectory(directory); frame.Save(Path.Combine(directory, $"safe-mode-options-{culture}.png"));
                toggle.IsChecked = false;
                Assert.IsFalse(preferences.Value.EnableSafeMode);
                Assert.HasCount(1, preferences.Value.MappedFolders);
            }
            finally { window.Close(); CultureInfo.CurrentUICulture = previous; }
            return true;
        }, CancellationToken.None).ContinueWith(task => task.GetAwaiter().GetResult(), TaskScheduler.Default);
    }

    internal static void AssertPolicy(XElement root)
    {
        Assert.IsNull(root.Element("MappedFolders"));
        Assert.IsEmpty(root.Descendants("MappedFolder"));
        Assert.AreEqual("Enable", root.Element("Networking")?.Value);
        foreach (var name in new[] { "ClipboardRedirection", "PrinterRedirection", "VideoInput", "AudioInput", "vGPU" })
            Assert.AreEqual("Disable", root.Element(name)?.Value, name);
    }

    private static PreferenceSettings AllSharingEnabled() => new()
    {
        UseAudioRedirection = true, UseVideoRedirection = true, UsePrinterRedirection = true,
        ShareNpkiFolder = true, EnableSandboxGpuAcceleration = true, EnableZScalerRootCertPropagation = true,
        EnableIdleAutoLogout = true, MappedFolders = [new() { HostFolder = "C:\\private-host-data" }],
        DataDirectoryHostPath = "\\\\server\\Data",
    };

    internal sealed class Preferences(PreferenceSettings value) : IPreferencesManager
    {
        public PreferenceSettings Value = value;
        public PreferenceSettings GetDefaultPreferences() => new();
        public Task<PreferenceSettings?> LoadPreferencesAsync(CancellationToken cancellationToken = default) => Task.FromResult<PreferenceSettings?>(Value);
        public Task SavePreferencesAsync(PreferenceSettings preferences, CancellationToken cancellationToken = default)
        { Value = preferences; return Task.CompletedTask; }
    }

    private sealed class Launcher : ISandboxLauncher
    {
        public TableClothConfiguration? Configuration;
        public Task<bool> RunSandboxAsync(TableClothConfiguration config, CancellationToken cancellationToken = default)
        { Configuration = config; return Task.FromResult(true); }
        public bool ValidateSandboxSpecFile(string wsbFilePath, out string? reason) { reason = null; return true; }
    }

    private sealed class Locations : ISharedLocations
    {
        public int MountChecks;
        public string GetEffectiveDataDirectoryPath(string? configuredPath) => configuredPath ?? "C:\\Data";
        public bool IsMountableDataDirectory(string? path) { MountChecks++; return false; }
        public string ExecutableDirectoryPath => throw new InvalidOperationException();
        public string ImagesZipFilePath => throw new InvalidOperationException();
        public string CatalogCacheFilePath => throw new InvalidOperationException();
        public string AppDataDirectoryPath => throw new InvalidOperationException();
        public string ApplicationLogPath => throw new InvalidOperationException();
        public string ExecutableFilePath => throw new InvalidOperationException();
        public string PreferencesFilePath => throw new InvalidOperationException();
        public string DefaultDataDirectoryPath => "C:\\Data";
        public string GetImageDirectoryPath() => throw new InvalidOperationException();
        public string GetImageFilePath(string serviceId) => throw new InvalidOperationException();
        public string GetIconFilePath(string serviceId) => throw new InvalidOperationException();
        public string GetTempPath() => throw new InvalidOperationException();
        public string GetHelpFilePath() => throw new InvalidOperationException();
        public string GetSandboxAppStagingPath(string sandboxStagingDirectory) => throw new InvalidOperationException();
    }
}
